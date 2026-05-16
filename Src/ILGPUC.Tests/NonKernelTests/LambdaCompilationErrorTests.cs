// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LambdaCompilationErrorTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using System;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

public sealed class LambdaCompilationErrorTests
{
    // Error kernel defined here (not in Kernels/) to avoid auto-discovery by KernelRegistry.
    // Creating a closure inside a loop body is unsupported: LowerGCInit detects the GCInit
    // node is inside a loop and throws NotSupportedException.
    private static class LambdaErrorKernels
    {
        public static void LambdaInLoopKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data)
        {
            int sum = 0;
            for (int i = 0; i < 4; i++)
            {
                int captured = i;          // new closure per iteration
                Func<int> f = () => captured;
                sum += f();
            }
            data[index] = sum;
        }
    }

    [Fact]
    public void LambdaInLoop_ThrowsNotSupportedException()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(LambdaErrorKernels).GetMethod(
            nameof(LambdaErrorKernels.LambdaInLoopKernel),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "Kernel method 'LambdaInLoopKernel' not found.");

        Assert.Throws<NotSupportedException>(() =>
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
    }
}
