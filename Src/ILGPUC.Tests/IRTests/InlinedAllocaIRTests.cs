// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlinedAllocaIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Vectors;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Kernels for <see cref="InlinedAllocaIRTests"/>. Declared outside the
/// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/> does
/// not add them to the shared backend corpus.
/// </summary>
static class InlinedAllocaKernels
{
    // Takes the local's address: forces an alloca in the caller and a cast at the
    // call site.
    static int Bump(ref int slot, int amount)
    {
        slot += amount;
        return slot;
    }

    // alloca -> store -> call -> load, in one inlinable body.
    static int Accumulate(int seed, int amount)
    {
        int acc = seed;
        int direct = Bump(ref acc, amount);
        return acc + direct;
    }

    public static void MergedAllocaChainKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int seed)
    {
        int total = 0;
        // The loop gives SimplifyControlFlow blocks to merge around the inliner's
        // split exit blocks.
        for (int i = 0; i < 3; ++i)
            total += Accumulate(seed + i, 5);
        data[index] = total;
    }

    static Float32x4 Step(Float32x4 value, int i) => i == 0 ? value + value : -value;

    static Float32x2 Pair(Float32x4 first, Float32x4 second) =>
        new(first.X, second.W);

    // The vector's constructor is inlined into the entry, the load after it is
    // demanded first by a call inlined in the loop body and later by a call
    // after the loop.
    public static void VectorAfterLoopKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        var x = Float32x4.FromElementView(data, index * 4);
        var y = Float32x4.FromElementView(data, index * 4 + 4);
        for (int i = 0; i < 2; ++i)
            Step(x, i).ToElementView(output, (index * 3 + i) * 4);
        var pair = Pair(x, y);
        output[index * 12 + 8] = pair.X;
        output[index * 12 + 9] = pair.Y;
    }
}

/// <summary>
/// Verifies that an address-taken local written, passed to an inlined call and read
/// back inside a loop is promoted as a single value.
/// </summary>
/// <remarks>
/// If inlining split the local's def-use set across two physical allocas, SSA
/// construction would promote the one the load points at and materialize its initial
/// value, so the seed parameter would stop participating in the arithmetic that
/// reaches the store. Asserting that the seed still feeds an <c>add</c> therefore
/// fails exactly when the def-use set splits.
/// </remarks>
public sealed class InlinedAllocaIRTests(ITestOutputHelper output)
    : CompilationTestBase(output)
{
    [Theory]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void MergedAllocaChain_SeedReachesAccumulation(OptimizationLevel opt)
    {
        var ir = DumpIRString(
            GetKernel(
                typeof(InlinedAllocaKernels),
                nameof(InlinedAllocaKernels.MergedAllocaChainKernel)),
            IRDumpPoint.AfterGlobalOpt,
            new CompilationProperties(OptimizationLevel: opt));

        // The seed parameter must still feed the arithmetic that reaches the store.
        Assert.Contains("add i32 %p2", ir);

        // The local must be fully promoted, which is what makes the value flow
        // above meaningful.
        Assert.DoesNotContain("alloca", ir);
    }

    /// <summary>
    /// A value moved to the exit block of an inlined call is created there when a
    /// call inlined later in another block demands it first, so a use after the
    /// loop still reads the loaded vector instead of the alloca's initial zero.
    /// </summary>
    [Theory]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void VectorAfterLoop_KeepsLoadedField(OptimizationLevel opt)
    {
        var ir = DumpIRString(
            GetKernel(
                typeof(InlinedAllocaKernels),
                nameof(InlinedAllocaKernels.VectorAfterLoopKernel)),
            IRDumpPoint.AfterGlobalOpt,
            new CompilationProperties(OptimizationLevel: opt));

        Assert.DoesNotContain("store float 0.0", ir);
    }
}
