// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LongBlockChainTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Vectors;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that kernels whose code turns into long chains of blocks compile on threads
/// with small stacks: the SSA construction looks up the values of variables through
/// the predecessors of a block without one stack frame per block.
/// </summary>
public sealed class LongBlockChainTests
{
    /// <summary>
    /// The stack size of the compiling thread, smaller than the stack of the main
    /// thread and of the test host.
    /// </summary>
    private const int StackSize = 1024 * 1024;

    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry. Every predicate tests all lanes with one branch per lane, so
    // the flags of three vectors become a chain of thousands of blocks, and the
    // stores after it look up the output view defined before it.
    private static class Kernels
    {
        private static int Flags(Float16x8 v) =>
            (Float16x8.IsNormal(v) ? 1 : 0) |
            (Float16x8.IsNaN(v) ? 2 : 0) |
            (Float16x8.IsInfinity(v) ? 4 : 0) |
            (Float16x8.IsFinite(v) ? 8 : 0) |
            (Float16x8.IsNegative(v) ? 16 : 0) |
            (Float16x8.IsSubnormal(v) ? 32 : 0) |
            (Float16x8.IsInteger(v) ? 64 : 0) |
            (Float16x8.IsZero(v) ? 128 : 0) |
            (Float16x8.IsPositive(v) ? 256 : 0) |
            (Float16x8.IsEvenInteger(v) ? 512 : 0) |
            (Float16x8.IsOddInteger(v) ? 1024 : 0) |
            (Float16x8.IsNegativeInfinity(v) ? 2048 : 0) |
            (Float16x8.IsPositiveInfinity(v) ? 4096 : 0);

        private static int Flags(Float32x8 v) =>
            (Float32x8.IsNormal(v) ? 1 : 0) |
            (Float32x8.IsNaN(v) ? 2 : 0) |
            (Float32x8.IsInfinity(v) ? 4 : 0) |
            (Float32x8.IsFinite(v) ? 8 : 0) |
            (Float32x8.IsNegative(v) ? 16 : 0) |
            (Float32x8.IsSubnormal(v) ? 32 : 0) |
            (Float32x8.IsInteger(v) ? 64 : 0) |
            (Float32x8.IsZero(v) ? 128 : 0) |
            (Float32x8.IsPositive(v) ? 256 : 0) |
            (Float32x8.IsEvenInteger(v) ? 512 : 0) |
            (Float32x8.IsOddInteger(v) ? 1024 : 0) |
            (Float32x8.IsNegativeInfinity(v) ? 2048 : 0) |
            (Float32x8.IsPositiveInfinity(v) ? 4096 : 0);

        public static void Float16x8FlagsKernel(
            Index1D index,
            ArrayView1D<Half, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> flags)
        {
            var v = Float16x8.FromElementView(data, index * 8);
            int a = Flags(v);
            int b = Flags(-v);
            int c = Flags((v - Float16x8.One) * v);
            flags[index * 3] = a;
            flags[index * 3 + 1] = b;
            flags[index * 3 + 2] = c;
        }

        public static void Float32x8FlagsKernel(
            Index1D index,
            ArrayView1D<float, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> flags)
        {
            var v = Float32x8.FromElementView(data, index * 8);
            int a = Flags(v);
            int b = Flags(-v);
            int c = Flags((v - Float32x8.One) * v);
            flags[index * 3] = a;
            flags[index * 3 + 1] = b;
            flags[index * 3 + 2] = c;
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> on a new thread with a stack of
    /// <see cref="StackSize"/> bytes and rethrows its exception, if any.
    /// </summary>
    private static void RunWithSmallStack(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
#pragma warning restore CA1031
                {
                    exception = ExceptionDispatchInfo.Capture(e);
                }
            },
            StackSize);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }

    [Theory]
    [InlineData(nameof(Kernels.Float16x8FlagsKernel), BackendType.CPU)]
    [InlineData(nameof(Kernels.Float16x8FlagsKernel), BackendType.Metal)]
    [InlineData(nameof(Kernels.Float32x8FlagsKernel), BackendType.CPU)]
    [InlineData(nameof(Kernels.Float32x8FlagsKernel), BackendType.Metal)]
    public void VectorPredicateFlags_CompileOnSmallStacks(
        string kernelName,
        BackendType backend)
    {
        var kernel = typeof(Kernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;

        string? source = null;
        RunWithSmallStack(() =>
        {
            using var helper = new CompilationHelper(backend);
            source = helper.GenerateBackendCode(kernel, backend).SourceCode;
        });

        Assert.False(string.IsNullOrEmpty(source));
    }
}
