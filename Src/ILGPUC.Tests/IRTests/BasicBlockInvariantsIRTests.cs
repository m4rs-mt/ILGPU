// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicBlockInvariantsIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using IRModule = ILGPUC.IR.ModuleValues.Module;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Structural invariants of sealed basic blocks: the cached phi and value
/// counts match the value chains the block actually holds, and every block
/// registers exactly one use of its termination value.
/// </summary>
public sealed class BasicBlockInvariantsIRTests
{
    /// <summary>
    /// Kernels compiled directly via <see cref="CompilationHelper"/>. This
    /// class is intentionally outside the <c>ILGPUC.Tests.Kernels</c>
    /// namespace so <see cref="KernelRegistry"/> does not enroll it in the
    /// backend test matrix.
    /// </summary>
    private static class InvariantKernels
    {
        /// <summary>
        /// Contains a loop (phi values) and a conditional branch.
        /// </summary>
        public static void LoopKernel(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; ++i)
                sum += i * data[index];
            if (sum > 16)
                sum -= 16;
            data[index] = sum;
        }

        /// <summary>
        /// Returns a loaded value, so the termination value of the block is a
        /// basic block value of the same block.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int LoadAndReturn(
            ArrayView1D<int, Stride1D.Dense> data, int index)
        {
            data[index + 1] = index;
            return data[index];
        }

        /// <summary>
        /// Branches on a loaded flag and returns a phi value.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int BranchOnLoad(
            ArrayView1D<bool, Stride1D.Dense> flags,
            ArrayView1D<int, Stride1D.Dense> data,
            int index)
        {
            int value = 0;
            if (flags[index])
                value = data[index];
            data[index + 1] = value;
            return value;
        }

        /// <summary>
        /// Calls helpers whose blocks terminate on their own block values.
        /// </summary>
        public static void TerminatorKernel(
            Index1D index,
            ArrayView1D<bool, Stride1D.Dense> flags,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int value = LoadAndReturn(data, index);
            value += BranchOnLoad(flags, data, index);
            data[index] = value;
        }
    }

    private static IRModule Compile(string name = nameof(InvariantKernels.LoopKernel))
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(InvariantKernels).GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static)!;
        return helper.CompileToModule(kernel, new CompilationProperties());
    }

    [Fact]
    public void ValueCounts_MatchEnumeratedValues()
    {
        var module = Compile();
        int numPhis = 0;
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            foreach (var block in method.Blocks)
            {
                int phis = 0;
                foreach (var _ in block.PhiValues)
                    ++phis;
                int values = 0;
                foreach (var _ in block.BasicBlockValues)
                    ++values;
                numPhis += phis;
                Assert.Equal(phis, block.NumPhiValues);
                Assert.Equal(values, block.NumPhiValues + block.NumValues);
            }
        }

        // The loop must actually produce phi values for the check to bite
        Assert.True(numPhis > 0, "expected phi values in the loop kernel");
    }

    [Theory]
    [InlineData(nameof(InvariantKernels.LoopKernel))]
    [InlineData(nameof(InvariantKernels.TerminatorKernel))]
    public void ValueCollection_CountMatchesDistinctEnumeratedValues(string kernel)
    {
        var module = Compile(kernel);
        int numBlocks = 0;
        int numChainTerminators = 0;
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            foreach (var block in method.Blocks)
            {
                ++numBlocks;
                var seen = new HashSet<ValueId>();
                int enumerated = 0;
                foreach (var value in block.Values)
                {
                    ++enumerated;
                    Assert.True(
                        seen.Add(value.Id),
                        $"{value} enumerated twice in {block.ToReferenceString()}");
                }
                Assert.Equal(enumerated, block.Values.Count);
                foreach (var (value, _) in block.BasicBlockValues)
                {
                    if (ReferenceEquals(value, block.TerminationValue))
                        ++numChainTerminators;
                }
            }
        }
        Assert.True(numBlocks > 0, "expected blocks");

        // Blocks terminating on one of their own values are the ones whose
        // termination value must not be counted twice
        Assert.True(
            numChainTerminators > 0,
            "expected blocks terminating on their own values");
    }

    [Fact]
    public void TerminationValue_HasSingleUsePerBlock()
    {
        var module = Compile();
        int numTerminators = 0;
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            foreach (var block in method.Blocks)
            {
                var terminator = block.TerminationValue;
                if (terminator is null)
                    continue;
                ++numTerminators;
                int uses = 0;
                foreach (var use in terminator.Uses)
                {
                    if (use.Target == block)
                        ++uses;
                }
                Assert.Equal(1, uses);
            }
        }
        Assert.True(numTerminators > 0, "expected terminated blocks");
    }

    [Fact]
    public void NumValueClasses_CoversEveryValueClass()
    {
        int numClasses = Enum.GetValues<ValueClass>().Count(c => c >= 0);
        Assert.Equal(ValueKinds.NumValueClasses, numClasses);
    }
}
