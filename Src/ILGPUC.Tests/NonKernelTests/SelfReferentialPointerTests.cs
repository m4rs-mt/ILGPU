// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SelfReferentialPointerTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that structures pointing to their own types, directly or through other
/// structures, have IR types and compile on all backends: the fields through
/// which a structure points to itself are opaque pointers in its type, and loads
/// from these fields convert their values back to the pointer types. Structures
/// holding arrays of themselves are rejected, and Metal and OpenCL reject the
/// addresses of locals stored to local structures.
/// </summary>
public sealed class SelfReferentialPointerTests
{
    #region Kernels

    private unsafe struct Node
    {
        public Node* Next;
        public int Value;
    }

    private struct NodePair
    {
        public Node First;
        public Node Second;
    }

    private unsafe struct Self
    {
        public Self* Me;
    }

    private unsafe struct Left
    {
        public Right* Other;
        public int Value;
    }

    private unsafe struct Right
    {
        public Left* Other;
        public int Value;
    }

    private struct ArrayNode
    {
        public ArrayNode[]? Children;
        public int Value;
    }

    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry
    private static unsafe class Kernels
    {
        public static void ListKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Node a, b, c;
            c.Next = null;
            c.Value = 100;
            b.Next = &c;
            b.Value = 10;
            a.Next = &b;
            a.Value = index + 1;

            int sum = 0;
            for (Node* node = &a; node != null; node = node->Next)
                sum += node->Value;
            data[index] = sum;
        }

        private static int ReadSecond(Node node) => node.Next[1].Value + node.Value;

        public static void ValueKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            NodePair pair;
            pair.First.Next = null;
            pair.First.Value = 3;
            pair.Second.Next = null;
            pair.Second.Value = index;

            Node head;
            head.Next = &pair.First;
            head.Value = 7;
            data[index] = ReadSecond(head);
        }

        public static void SelfKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Self self;
            self.Me = &self;
            data[index] = self.Me->Me == &self ? 2 * index : -1;
        }

        public static void MutualKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Left left;
            Right right;
            left.Other = &right;
            left.Value = index;
            right.Other = &left;
            right.Value = 100;
            data[index] = left.Other->Other->Value + left.Other->Value;
        }

        public static void ArrayKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            ArrayNode node = default; // ArrayNode
            node.Children = null;
            node.Value = index;
            data[index] = node.Value;
        }
    }

    private static MethodInfo GetKernel(string name) =>
        typeof(Kernels).GetMethod(name, BindingFlags.Public | BindingFlags.Static)
        ?? throw new ArgumentException($"Kernel '{name}' not found");

    #endregion

    #region Types

    [Fact]
    public unsafe void SelfReferentialStructure_HasOpaquePointerField()
    {
        var builder = new ModuleBuilder(
            new CompilationProperties(),
            new Generation(),
            Location.Unknown,
            new TypeInformationManager());

        var nodeType = Assert.IsType<StructureType>(
            builder.CreateType(typeof(Node)));
        var nextType = Assert.IsType<PointerType>(nodeType.DirectFields[0]);
        Assert.IsType<VoidType>(nextType.ElementType);

        var pointerType = Assert.IsType<PointerType>(
            builder.CreateType(typeof(Node*)));
        Assert.Equal(nodeType, pointerType.ElementType);
    }

    #endregion

    #region Backends

    /// <summary>
    /// Returns true if the given backend rejects the addresses of locals that
    /// kernels store to memory, which these kernels store to local structures.
    /// </summary>
    private static bool RejectsStoredLocalAddresses(BackendType backend) =>
        backend is BackendType.Metal or BackendType.OpenCL;

    public static TheoryData<BackendType, string> KernelsOnAllBackends()
    {
        var data = new TheoryData<BackendType, string>();
        foreach (var backend in Enum.GetValues<BackendType>())
        {
            data.Add(backend, nameof(Kernels.ValueKernel));
            if (RejectsStoredLocalAddresses(backend))
                continue;
            data.Add(backend, nameof(Kernels.ListKernel));
            data.Add(backend, nameof(Kernels.SelfKernel));
            data.Add(backend, nameof(Kernels.MutualKernel));
        }
        return data;
    }

    public static TheoryData<BackendType, string> StoredLocalAddressKernels()
    {
        var data = new TheoryData<BackendType, string>();
        foreach (var backend in Enum.GetValues<BackendType>())
        {
            if (!RejectsStoredLocalAddresses(backend))
                continue;
            data.Add(backend, nameof(Kernels.ListKernel));
            data.Add(backend, nameof(Kernels.SelfKernel));
            data.Add(backend, nameof(Kernels.MutualKernel));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(KernelsOnAllBackends))]
    public void SelfReferentialStructure_GeneratesSource(
        BackendType backend,
        string kernel)
    {
        using var helper = new CompilationHelper(backend);
        var result = AssertionRecorder.Run(
            () => helper.GenerateBackendCode(GetKernel(kernel), backend));
        Assert.False(string.IsNullOrWhiteSpace(result.SourceCode));
    }

    /// <summary>
    /// Backends without generic pointers reject the kernels that store the
    /// addresses of locals to local structures with a located diagnostic.
    /// </summary>
    [Theory]
    [MemberData(nameof(StoredLocalAddressKernels))]
    public void SelfReferentialStructure_StoredLocalAddress_ReportsError(
        BackendType backend,
        string kernel)
    {
        using var helper = new CompilationHelper(backend);
        var exception = Assert.Throws<NotSupportedException>(
            () => helper.GenerateBackendCode(GetKernel(kernel), backend));
        Assert.Contains($"Kernel 'Kernels.{kernel}'", exception.Message);
        Assert.Contains(
            "points to local memory but is stored to memory as a generic pointer",
            exception.Message);
        Assert.Contains("SelfReferentialPointerTests.cs', line ", exception.Message);
        Assert.Contains(
            $"{backend} does not support generic pointers",
            exception.Message);
    }

    #endregion

    #region Diagnostics

    [Fact]
    public void SelfReferentialArray_ReportsUnsupportedConstruct()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var exception = Assert.Throws<InternalCompilerException>(() =>
            helper.GenerateBackendCode(
                GetKernel(nameof(Kernels.ArrayKernel)),
                BackendType.CPU));

        Assert.True(KernelCompilationErrors.TryCreateDiagnostic(
            exception,
            Microsoft.CodeAnalysis.Location.None,
            out var diagnostic));
        Assert.Equal("ILGPU1200", diagnostic!.Id);
        Assert.Contains("+ArrayNode[]' is not supported", diagnostic.Message);

        // The local is created with the kernel, at the first sequence point of
        // the kernel: its opening brace in debug builds or the declaration
        int line = FindMarkerLine("ArrayNode");
        Assert.InRange(
            Assert.IsAssignableFrom<FileLocation>(exception.SourceLocation).StartLine,
            line - 1,
            line);
    }

    /// <summary>
    /// Returns the line of this file marked with the given comment.
    /// </summary>
    private static int FindMarkerLine(
        string marker,
        [CallerFilePath] string sourcePath = "")
    {
        int line = Array.FindIndex(
            File.ReadAllLines(sourcePath),
            l => l.EndsWith("// " + marker, StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, $"Marker '{marker}' not found");
        return line;
    }

    #endregion
}
