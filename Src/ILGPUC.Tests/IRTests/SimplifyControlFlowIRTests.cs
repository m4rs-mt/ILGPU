// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SimplifyControlFlowIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using IRModule = ILGPUC.IR.ModuleValues.Module;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Tests the empty-block cleanup of <see cref="SimplifyControlFlow"/>: empty
/// forwarding blocks that are no phi sources are removed, also when they only
/// become empty in SSA construction at O0, while empty blocks that are phi
/// sources (split edges into merge blocks) and empty endless loops are kept.
/// </summary>
public sealed class SimplifyControlFlowIRTests : DisposeBase
{
    private readonly CompilationHelper _helper = new();

    #region Snippets

    private static class Snippets
    {
        /// <summary>
        /// The inner if-else joins in an empty block without phis, which only
        /// forwards to the join block of the outer if.
        /// </summary>
        public static void NestedJoin(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data, int bound)
        {
            if (index < bound)
            {
                if (data[index] > 0)
                    data[index] = 1;
                else
                    data[index + 1] = 2;
            }
        }

        /// <summary>
        /// The inner if-else joins in a block that merges a value, so the empty
        /// edge blocks into the joins are phi sources and must be kept.
        /// </summary>
        public static void NestedPhiJoin(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data, int bound)
        {
            int value = 0;
            if (index < bound)
            {
                if (data[index] > 0)
                    value = data[index + 1];
                else
                    data[index + 1] = 2;
            }
            data[index] = value;
        }

        /// <summary>
        /// Writes a value and spins forever on one side, so the method keeps an
        /// empty endless loop.
        /// </summary>
        public static void EmptyEndlessLoop(
            Index1D index, ArrayView1D<int, Stride1D.Dense> data, int bound)
        {
            if (bound > 5)
            {
                data[index] = 1;
                while (true)
                {
                }
            }
            data[index] = 2;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Compiles the method through the full pipeline of the given level.
    /// </summary>
    private IRModule Optimize(
        MethodInfo method,
        OptimizationLevel level = OptimizationLevel.O1,
        CompilationMode mode = CompilationMode.Release)
    {
        var props = new CompilationProperties(OptimizationLevel: level)
            .WithMode(mode);
        var module = _helper.CompileToModule(method, props);
        return level.CreateTransformer().Apply(props, _helper.TypeManager, module);
    }

    /// <summary>
    /// Returns all empty blocks that only forward to a single successor without
    /// phi values and are no phi sources themselves.
    /// </summary>
    private static List<string> FindRemovableEmptyBlocks(IRModule module)
    {
        var result = new List<string>();
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            var phiSources = method.Blocks.ComputePhiSources();
            foreach (var block in method.Blocks)
            {
                if (block == method.EntryBlock ||
                    block.TerminationKind != BlockTerminationKind.Unconditional ||
                    block.FirstValue is not null ||
                    block.FirstPhiValue is not null ||
                    phiSources.Contains(block))
                {
                    continue;
                }

                var successor = block.Successors[0];
                if (successor != block && successor.FirstPhiValue is null)
                    result.Add($"{method}: {block.ToReferenceString()}");
            }
        }
        return result;
    }

    /// <summary>
    /// Returns the number of empty blocks that are phi sources.
    /// </summary>
    private static int CountEmptyPhiSources(IRModule module)
    {
        int count = 0;
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            var phiSources = method.Blocks.ComputePhiSources();
            foreach (var block in method.Blocks)
            {
                if (block.FirstValue is null &&
                    block.FirstPhiValue is null &&
                    phiSources.Contains(block))
                {
                    ++count;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Returns true if a block of a method with an implementation lies on a
    /// cycle.
    /// </summary>
    private static bool HasCycle(IRModule module)
    {
        foreach (var method in module.Methods)
        {
            if (!method.HasImplementation)
                continue;
            foreach (var block in method.Blocks)
            {
                var visited = new HashSet<BasicBlock>();
                var stack = new Stack<BasicBlock>(block.Successors.ToArray());
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (current == block)
                        return true;
                    if (!visited.Add(current))
                        continue;
                    foreach (var successor in current.Successors)
                        stack.Push(successor);
                }
            }
        }
        return false;
    }

    private static MethodInfo GetSnippet(string name) =>
        typeof(Snippets).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;

    #endregion

    [Fact]
    public void NestedJoin_RemovesEmptyForwardingBlock()
    {
        var module = Optimize(GetSnippet(nameof(Snippets.NestedJoin)));
        Assert.Empty(FindRemovableEmptyBlocks(module));
    }

    [Fact]
    public void SharedMemoryArrayKernel_RemovesEmptyForwardingBlock()
    {
        var module = Optimize(typeof(SharedMemoryKernels).GetMethod(
            nameof(SharedMemoryKernels.SharedMemoryArrayKernel))!);
        Assert.Empty(FindRemovableEmptyBlocks(module));
    }

    [Fact]
    public void NestedPhiJoin_KeepsEmptyPhiSourceBlocks()
    {
        var module = Optimize(GetSnippet(nameof(Snippets.NestedPhiJoin)));
        Assert.Empty(FindRemovableEmptyBlocks(module));
        Assert.True(
            CountEmptyPhiSources(module) > 0,
            "expected empty edge blocks that are phi sources");
    }

    [Theory]
    [InlineData(CompilationMode.Debug)]
    [InlineData(CompilationMode.Release)]
    public void TwoClosuresCrossBlockKernel_O0_RemovesBlocksEmptiedBySSA(
        CompilationMode mode)
    {
        var module = Optimize(
            typeof(LambdaKernels).GetMethod(
                nameof(LambdaKernels.TwoClosuresCrossBlockKernel))!,
            OptimizationLevel.O0,
            mode);
        Assert.Empty(FindRemovableEmptyBlocks(module));
        Assert.True(
            CountEmptyPhiSources(module) > 0,
            "expected empty edge blocks that are phi sources");
    }

    [Fact]
    public void EmptyEndlessLoop_O0_KeepsTheLoop()
    {
        var module = Optimize(
            GetSnippet(nameof(Snippets.EmptyEndlessLoop)),
            OptimizationLevel.O0);
        Assert.True(HasCycle(module), "expected the endless loop to remain");
    }

    protected override void Dispose(bool disposing)
    {
        _helper.Dispose();
        base.Dispose(disposing);
    }
}
