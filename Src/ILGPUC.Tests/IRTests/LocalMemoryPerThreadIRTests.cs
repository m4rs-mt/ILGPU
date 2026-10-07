// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalMemoryPerThreadIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Checks that local memory per thread is backed by a per-thread global on
/// every backend.
/// </summary>
public sealed class LocalMemoryPerThreadIRTests(ITestOutputHelper output) :
    CompilationTestBase(output)
{
    /// <summary>
    /// Fills local memory per thread in a loop and sums it up.
    /// </summary>
    internal static void Local1DKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        var local = Group.GetLocalMemoryPerThread<int>(5);
        for (int i = 0; i < 5; ++i)
            local[i] = global + i;
        int sum = 0;
        for (int i = 0; i < 5; ++i)
            sum += local[i];
        output[global] = sum + local.IntLength;
    }

    /// <summary>
    /// Writes 2D local memory per thread at an index that depends on the thread.
    /// </summary>
    internal static void Local2DKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        var grid = Group.GetLocalMemoryPerThread2D<int, Stride2D.DenseX>(
            new Index2D(3, 2));
        var index = new Index2D(global % 3, 1);
        grid[index] = global;
        output[global] = grid[index];
    }

    /// <summary>
    /// The view returned for local memory keeps pointing to its global, which
    /// the view's escape keeps from being promoted to registers.
    /// </summary>
    [Fact]
    public void LocalMemoryView_KeepsItsGlobal()
    {
        var ir = DumpIRString(
            GetKernel(typeof(LocalMemoryPerThreadIRTests), nameof(Local1DKernel)),
            IRDumpPoint.AfterGlobalOpt);
        Assert.Contains("@ilgpu.view.new(ptr @g0, i64 5)", ir);
        Assert.DoesNotContain("ptr null", ir);
    }

    /// <summary>
    /// The 2D local memory, whose length inlining makes a constant, is a
    /// per-thread global of that length on every backend.
    /// </summary>
    [Theory]
    [InlineData(BackendType.CPU)]
    [InlineData(BackendType.Metal)]
    [InlineData(BackendType.Cuda)]
    [InlineData(BackendType.ROCm)]
    [InlineData(BackendType.OpenCL)]
    public void LocalMemory2D_IsGlobal(BackendType backend)
    {
        var kernel = GetKernel(
            typeof(LocalMemoryPerThreadIRTests),
            nameof(Local2DKernel));
        using var helper = new CompilationHelper(backend);
        var ir = helper.GetNormalizedIR(
            kernel,
            IRDumpPoint.AfterBackendTransforms,
            backend: backend);
        Assert.Contains("internal global i32 zeroinitializer ; local", ir);
        Assert.DoesNotContain("alloca i32, i32 %", ir);
    }
}
