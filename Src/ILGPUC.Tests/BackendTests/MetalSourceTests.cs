// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalSourceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Checks that the Metal backend rejects what Metal cannot express with a
/// clear compile-time error instead of emitting invalid source.
/// </summary>
public sealed class MetalSourceTests
{
    /// <summary>
    /// Computes in double precision on float buffers.
    /// </summary>
    internal static void DoubleArithmeticKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data)
    {
        double value = data[index];
        data[index] = (float)(value * 0.5 + 1.0);
    }

    /// <summary>
    /// Metal has no 64-bit floats.
    /// </summary>
    [Fact]
    public void Double_ReportsError()
    {
        var kernel = typeof(MetalSourceTests)
            .GetMethod(
                nameof(DoubleArithmeticKernel),
                BindingFlags.NonPublic | BindingFlags.Static)!;
        using var helper = new CompilationHelper(BackendType.Metal);

        var exception = Assert.ThrowsAny<Exception>(
            () => helper.GenerateBackendCode(kernel, BackendType.Metal));
        Assert.Contains("64-bit floating-point", Flatten(exception));
    }

    /// <summary>
    /// Metal has no 64-bit atomics.
    /// </summary>
    [Fact]
    public void Int64Atomics_ReportError()
    {
        var kernel = typeof(AtomicKernels)
            .GetMethod(nameof(AtomicKernels.AtomicInt64Kernel))!;
        using var helper = new CompilationHelper(BackendType.Metal);

        var exception = Assert.ThrowsAny<Exception>(
            () => helper.GenerateBackendCode(kernel, BackendType.Metal));
        Assert.Contains("not supported by Metal", Flatten(exception));
    }

    private static string Flatten(Exception exception)
    {
        var message = string.Empty;
        for (Exception? current = exception; current is not null;
            current = current.InnerException)
        {
            message += current.Message + Environment.NewLine;
        }
        return message;
    }
}
