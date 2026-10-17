// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureLayoutIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

namespace ILGPUC.Tests.IRTests;

// The fields of these structures are only accessed in kernels or by their
// offsets, so the C# compiler cannot see their assignments.
#pragma warning disable CS0649
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct LayoutPack1
{
    public sbyte A;
    public int B;
    public long C;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
struct LayoutPack2
{
    public byte A;
    public int B;
    public long C;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
struct LayoutPack4
{
    public short A;
    public long B;
    public byte C;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct LayoutPackedTail
{
    public int A;
    public byte B;
}

[StructLayout(LayoutKind.Sequential, Size = 10)]
struct LayoutOddSize
{
    public int A;
    public short B;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct LayoutPackedInts
{
    public int A;
    public int B;
}

struct LayoutWithPackedInts
{
    public byte Tag;
    public LayoutPackedInts Inner;
}

[StructLayout(LayoutKind.Sequential, Size = 24)]
struct LayoutSized
{
    public int A;
    public short B;
}

struct LayoutWithSized
{
    public byte Tag;
    public LayoutSized Inner;
    public short S;
}

[StructLayout(LayoutKind.Explicit)]
struct LayoutExplicit
{
    [FieldOffset(0)] public int A;
    [FieldOffset(4)] public int B;
}
#pragma warning restore CS0649

/// <summary>
/// Kernels for <see cref="StructureLayoutIRTests"/>. Declared outside the
/// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/> does
/// not add them to the shared backend corpus.
/// </summary>
static class StructureLayoutKernels
{
    public static void SizedParameterKernel(
        Index1D i,
        ArrayView1D<long, Stride1D.Dense> d,
        LayoutWithSized value) =>
        d[i] = value.Tag + value.Inner.A + value.Inner.B + value.S;

    public static void Pack1Kernel(
        Index1D i,
        ArrayView1D<LayoutPack1, Stride1D.Dense> d) =>
        d[i].B += 1; // Pack1Kernel

    public static void Pack2Kernel(
        Index1D i,
        ArrayView1D<LayoutPack2, Stride1D.Dense> d) =>
        d[i].B += 1; // Pack2Kernel

    public static void Pack4Kernel(
        Index1D i,
        ArrayView1D<LayoutPack4, Stride1D.Dense> d) =>
        d[i].B += 1; // Pack4Kernel

    public static void PackedTailKernel(
        Index1D i,
        ArrayView1D<LayoutPackedTail, Stride1D.Dense> d) =>
        d[i].A += 1; // PackedTailKernel

    public static void OddSizeKernel(
        Index1D i,
        ArrayView1D<LayoutOddSize, Stride1D.Dense> d) =>
        d[i].A += 1; // OddSizeKernel

    public static void NestedPackKernel(
        Index1D i,
        ArrayView1D<LayoutWithPackedInts, Stride1D.Dense> d) =>
        d[i].Inner.B += 1; // NestedPackKernel

    public static void ByValueKernel(
        Index1D i,
        ArrayView1D<int, Stride1D.Dense> d,
        LayoutPack2 value) =>
        d[i] = value.B; // ByValueKernel

    public static void ExplicitKernel(
        Index1D i,
        ArrayView1D<LayoutExplicit, Stride1D.Dense> d) =>
        d[i].B += 1; // ExplicitKernel
}

/// <summary>
/// Verifies that structure types keep the layout they have on the host, and that
/// structure types whose packing or explicit size moves fields away from their
/// natural alignments, or gives them a size that is not a multiple of their
/// alignment, are rejected with ILGPU1200 at the location of the kernel on every
/// backend instead of being laid out differently than on the host.
/// </summary>
public sealed class StructureLayoutIRTests
{
    private static readonly RoslynLocation LaunchSite = RoslynLocation.Create(
        "/src/Launch.cs",
        default,
        new LinePositionSpan(new LinePosition(4, 8), new LinePosition(4, 20)));

    /// <summary>
    /// Compiles the given kernel, which must be rejected with ILGPU1200 at the
    /// line of this file marked with the name of the kernel, and returns the
    /// message of the diagnostic.
    /// </summary>
    private static string Compile(
        string kernelName,
        BackendType backend,
        [CallerFilePath] string sourcePath = "")
    {
        var kernel = typeof(StructureLayoutKernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        using var helper = new CompilationHelper(backend);
        var exception = Assert.ThrowsAny<Exception>(
            () => helper.GenerateBackendCode(kernel, backend));
        Assert.True(KernelCompilationErrors.TryCreateDiagnostic(
            exception,
            LaunchSite,
            out var diagnostic));
        Assert.Equal("ILGPU1200", diagnostic!.Id);

        var span = diagnostic.Location.GetLineSpan();
        Assert.Equal(Path.GetFileName(sourcePath), Path.GetFileName(span.Path));
        var lines = File.ReadAllLines(sourcePath);
        int line = Array.FindIndex(
            lines,
            l => l.EndsWith("// " + kernelName, StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, $"Marker '{kernelName}' not found");
        Assert.Equal(line, span.StartLinePosition.Line + 1);
        return diagnostic.Message;
    }

    /// <summary>
    /// A parameter of a structure that contains an explicitly sized structure
    /// keeps the offsets of its fields through optimization: the fields behind
    /// the nested structure stay behind its explicit size.
    /// </summary>
    [Theory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void SizedNestedStructureParameter_KeepsLayout(OptimizationLevel level)
    {
        var kernel = typeof(StructureLayoutKernels).GetMethod(
            nameof(StructureLayoutKernels.SizedParameterKernel))!;
        var properties = new CompilationProperties { OptimizationLevel = level };
        using var helper = new CompilationHelper();
        var module = level.CreateTransformer().Apply(
            properties,
            helper.TypeManager,
            helper.CompileToModule(kernel, properties));

        Parameter? value = null;
        foreach (var parameter in module.EntryPoint.Parameters)
        {
            if (parameter.Name == "value")
                value = parameter;
        }
        var type = Assert.IsType<StructureType>(value?.Type);
        Assert.Equal(Unsafe.SizeOf<LayoutWithSized>(), type.Size);
        Assert.Equal(
            (int)Marshal.OffsetOf<LayoutWithSized>(nameof(LayoutWithSized.S)),
            type.GetOffset(new(type.NumFields - 1)));
    }

    /// <summary>
    /// Packing that moves fields, packing that shortens the size, explicit sizes
    /// that are no multiple of the alignment and packed structures nested at
    /// offsets their natural alignment would not give them are rejected, also
    /// for by-value parameters.
    /// </summary>
    [Theory]
    [InlineData(nameof(StructureLayoutKernels.Pack1Kernel), typeof(LayoutPack1))]
    [InlineData(nameof(StructureLayoutKernels.Pack2Kernel), typeof(LayoutPack2))]
    [InlineData(nameof(StructureLayoutKernels.Pack4Kernel), typeof(LayoutPack4))]
    [InlineData(
        nameof(StructureLayoutKernels.PackedTailKernel),
        typeof(LayoutPackedTail))]
    [InlineData(nameof(StructureLayoutKernels.OddSizeKernel), typeof(LayoutOddSize))]
    [InlineData(
        nameof(StructureLayoutKernels.NestedPackKernel),
        typeof(LayoutWithPackedInts))]
    [InlineData(nameof(StructureLayoutKernels.ByValueKernel), typeof(LayoutPack2))]
    public void NonNaturalLayout_IsRejected(string kernelName, Type type)
    {
        foreach (var backend in new[]
        {
            BackendType.CPU,
            BackendType.Cuda,
            BackendType.Metal,
            BackendType.OpenCL,
            BackendType.ROCm,
        })
        {
            var message = Compile(kernelName, backend);
            Assert.Contains($"'{type.FullName}'", message);
            Assert.Contains("Pack and Size", message);
        }
    }

    /// <summary>
    /// Explicit layouts are rejected.
    /// </summary>
    [Fact]
    public void ExplicitLayout_IsRejected()
    {
        var message = Compile(
            nameof(StructureLayoutKernels.ExplicitKernel),
            BackendType.CPU);
        Assert.Contains(
            $"'{typeof(LayoutExplicit).FullName}' has an unsupported structure layout",
            message);
    }
}
