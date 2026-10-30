// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FormatArgumentTypeTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that arguments of <see cref="Interop.Write{T1}(string, T1)"/> and of inline
/// PTX whose types cannot be formatted are rejected with the C# name of their type.
/// </summary>
public sealed class FormatArgumentTypeTests
{
    /// <summary>
    /// A structure that cannot be formatted.
    /// </summary>
    public struct Pair
    {
        public int A;
        public float B;
    }

    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry
    private static class Kernels
    {
        public static void WriteStructureKernel(
            Index1D index,
            ArrayView1D<Pair, Stride1D.Dense> data)
        {
            Interop.Write("{0}", data[index]);
        }

        public static void EmitStructureKernel(
            Index1D index,
            ArrayView1D<Pair, Stride1D.Dense> data)
        {
            CudaAsm.Emit("mov.b32 %0, %0;", data[index]);
        }
    }

    /// <summary>
    /// Compiles <paramref name="kernelName"/> for <paramref name="backend"/> and
    /// returns the messages of the exception chain of its compile error.
    /// </summary>
    private static string GetCompileErrorMessages(
        string kernelName,
        BackendType backend)
    {
        var kernel = typeof(Kernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        using var helper = new CompilationHelper(backend);
        var exception = Assert.ThrowsAny<Exception>(() =>
            helper.GenerateBackendCode(kernel, backend));

        var messages = new List<string>();
        for (var current = exception;
             current is not null;
             current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join("\n", messages);
    }

    [Fact]
    public void WriteStructure_NamesTheType() =>
        Assert.Contains(
            "Not supported write format argument type 'Pair' in '{0}'",
            GetCompileErrorMessages(
                nameof(Kernels.WriteStructureKernel),
                BackendType.CPU),
            StringComparison.Ordinal);

    [Fact]
    public void EmitStructure_NamesTheType() =>
        Assert.Contains(
            "Not supported inline PTX format argument type 'Pair' in " +
            "'mov.b32 %0, %0;'",
            GetCompileErrorMessages(
                nameof(Kernels.EmitStructureKernel),
                BackendType.Cuda),
            StringComparison.Ordinal);
}
