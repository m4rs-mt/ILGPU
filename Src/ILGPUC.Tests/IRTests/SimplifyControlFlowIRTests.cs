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
using System.Reflection;
using Xunit;
using IRModule = ILGPUC.IR.ModuleValues.Module;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Tests the empty-block cleanup of <see cref="SimplifyControlFlow"/>: empty
/// forwarding blocks that are no phi sources are removed, while empty blocks
/// that are phi sources (split edges into merge blocks) are kept.
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
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Compiles the method through the full O1 pipeline.
    /// </summary>
    private IRModule Optimize(MethodInfo method)
    {
        var props = new CompilationProperties(
            OptimizationLevel: OptimizationLevel.O1)
            .WithMode(CompilationMode.Release);
        var module = _helper.CompileToModule(method, props);
        return OptimizationLevel.O1.CreateTransformer()
            .Apply(props, _helper.TypeManager, module);
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

    protected override void Dispose(bool disposing)
    {
        _helper.Dispose();
        base.Dispose(disposing);
    }
}
