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

        // Delegates returned from methods cannot be traced to their lambdas.
        private static Func<int, int> Select(int flag) =>
            flag > 0 ? x => x + 1 : x => x * 2;

        public static void ReturnedDelegateKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data, int flag)
        {
            var f = Select(flag);
            data[index] = f(index.X);
        }

        private static int Apply(Func<int, int> f, int x) => f(x);

        // A helper (not the kernel) passes a returned delegate to a method taking
        // a Func, which cannot be specialized for it.
        private static int ApplyReturned(int flag, int x) => Apply(Select(flag), x);

        public static void ReturnedDelegateArgumentKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data, int flag)
        {
            data[index] = ApplyReturned(flag, index.X);
        }
    }

    private static MethodInfo GetKernel(string name) =>
        typeof(LambdaErrorKernels).GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Kernel method '{name}' not found.");

    [Fact]
    public void LambdaInLoop_ReportsClosureInLoop()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(LambdaErrorKernels).GetMethod(
            nameof(LambdaErrorKernels.LambdaInLoopKernel),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "Kernel method 'LambdaInLoopKernel' not found.");

        var exception = Assert.Throws<KernelCompilationException>(() =>
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
        Assert.Equal("ILGPU1208", exception.Id);
    }

    [Fact]
    public void ReturnedDelegate_ReportsUnresolvedDelegate()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = GetKernel(nameof(LambdaErrorKernels.ReturnedDelegateKernel));

        var exception = Assert.Throws<KernelCompilationException>(() =>
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
        Assert.Equal("ILGPU1202", exception.Id);
        Assert.Contains(
            "invoked in method 'ReturnedDelegateKernel': it is returned from " +
            "method 'Select'",
            exception.Message);
    }

    [Fact]
    public void ReturnedDelegateArgument_ReportsUnresolvedDelegate()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = GetKernel(
            nameof(LambdaErrorKernels.ReturnedDelegateArgumentKernel));

        // Reported although the call is in a helper instead of the kernel
        var exception = Assert.Throws<KernelCompilationException>(() =>
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
        Assert.Equal("ILGPU1202", exception.Id);
        Assert.Contains(
            "parameter 'f' of method 'Apply': it is returned from method 'Select'",
            exception.Message);
    }
}
