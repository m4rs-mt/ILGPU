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
}
