// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IfConditionConversionIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Unit-level tests of the IfConditionConversion region detection
/// (<c>IfConditionConversion.FindRegions</c>): accepted/rejected regions, exit
/// counts, and the small-budget rejection paths — asserted without running the
/// rewrite itself. Methods are compiled through the frontend and the
/// pre-conditional O1 pipeline prefix (DebugSetter + basic optimizations), i.e.
/// exactly the input the pass sees in the real pipeline.
/// </summary>
public sealed class IfConditionConversionIRTests : DisposeBase
{
    private readonly CompilationHelper _helper = new();

    #region Snippets

    private static class Snippets
    {
        public static int AndChain(int a, int b, int c)
        {
            if (a > 0 && b > 0 && c > 0)
                return 1;
            return 2;
        }

        public static int DivGuard(int a, int b)
        {
            if (b != 0 && a / b > 2)
                return 1;
            return 0;
        }

        public static int DivGuardPhi(int a, int b) => b != 0 ? a / b : 0;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Compiles the method and runs the O1 pipeline prefix that precedes
    /// AddConditionalOptimizations (DebugSetter + basic optimizations in
    /// Release mode), returning the entry-point method of the result.
    /// </summary>
    private Method PrepareMethod(MethodInfo method)
    {
        var props = new CompilationProperties(
            OptimizationLevel: OptimizationLevel.O1)
            .WithMode(CompilationMode.Release);
        var module = _helper.CompileToModule(method, props);

        var builder = Transformer.CreateBuilder();
        builder.AddDebugSetter();
        builder.AddBasicOptimizations();
        var optimized = builder
            .ToTransformer()
            .Apply(props, _helper.TypeManager, module);

        return optimized.EntryPoint;
    }

    private static MethodInfo GetSnippet(string name) =>
        typeof(Snippets).GetMethod(
            name, BindingFlags.Public | BindingFlags.Static).AsNotNull();

    /// <summary>
    /// Asserts that no accepted region would speculate an integer division:
    /// walks exactly the DAGs the pass makes unconditional — non-entry branch
    /// conditions, inner-phi arguments, and exit-phi region arguments.
    /// </summary>
    private static void AssertNoSpeculatedDivision(
        List<IfConditionConversion.RegionInfo> regions)
    {
        var visited = new HashSet<Value>();

        static bool ContainsDivision(Value? value, HashSet<Value> visited)
        {
            if (value is not PureValue pureValue || !visited.Add(pureValue))
                return false;
            if (pureValue is BinaryArithmeticValue binary &&
                (binary.Kind == BinaryArithmeticKind.Div ||
                 binary.Kind == BinaryArithmeticKind.Rem))
            {
                return true;
            }
            foreach (var operand in pureValue.Values)
            {
                if (ContainsDivision(operand, visited))
                    return true;
            }
            return false;
        }

        foreach (var region in regions)
        {
            foreach (var block in region.Blocks)
            {
                if (block == region.Entry)
                    continue;
                Assert.False(
                    ContainsDivision(block.TerminationCondition, visited),
                    "Region speculates a division via a branch condition");
                foreach (PhiValue phi in block.PhiValues)
                {
                    foreach (var argument in phi.Arguments)
                    {
                        Assert.False(
                            ContainsDivision(argument, visited),
                            "Region speculates a division via an inner phi");
                    }
                }
            }
            foreach (var exit in region.Exits)
            {
                foreach (PhiValue phi in exit.PhiValues)
                {
                    for (int i = 0; i < phi.NumArguments; ++i)
                    {
                        if (!region.Blocks.Contains(phi.Sources[i]))
                            continue;
                        Assert.False(
                            ContainsDivision(phi.Arguments[i], visited),
                            "Region speculates a division via an exit phi");
                    }
                }
            }
            visited.Clear();
        }
    }

    #endregion

    #region Tests

    [Fact]
    public void AndChain_IsDetectedAsSingleRegion()
    {
        var method = PrepareMethod(GetSnippet(nameof(Snippets.AndChain)));
        var regions = IfConditionConversion.FindRegions(method, 64, 256);

        var region = Assert.Single(regions);
        // All paths converge on the unified exit block — the branch chain
        // collapses to a single-exit region (the branch disappears entirely).
        Assert.Single(region.Exits);
        // The region contains at least the three conditional chain blocks.
        Assert.True(region.Blocks.Count >= 3);
    }

    [Fact]
    public void DivGuard_NeverSpeculatesTheDivision()
    {
        // Speculating a / b above its b != 0 guard would fault — the hard
        // legality rule must reject the guarded region (branch-condition or
        // stack-merge phi DAG form, depending on the test build's IL shape).
        // Downstream regions without the division may legitimately fold.
        var method = PrepareMethod(GetSnippet(nameof(Snippets.DivGuard)));
        var regions = IfConditionConversion.FindRegions(method, 64, 256);
        AssertNoSpeculatedDivision(regions);
    }

    [Fact]
    public void DivGuardPhi_NeverSpeculatesTheDivision()
    {
        // Same legality rule, but the division occurs as a phi ARGUMENT of the
        // guarded merge — exercises the phi-argument speculation check.
        var method = PrepareMethod(GetSnippet(nameof(Snippets.DivGuardPhi)));
        var regions = IfConditionConversion.FindRegions(method, 64, 256);
        AssertNoSpeculatedDivision(regions);
        Assert.Empty(regions);
    }

    [Fact]
    public void AndChain_TinyBlockBudget_RejectsTheChainRegion()
    {
        var method = PrepareMethod(GetSnippet(nameof(Snippets.AndChain)));
        var full = Assert.Single(
            IfConditionConversion.FindRegions(method, 64, 256));
        var restricted = IfConditionConversion.FindRegions(
            method, maxInnerBlocks: 1, maxSpeculatedValues: 256);
        // The full chain region must be gone; at most tiny sub-regions
        // (entry + one inner block) may survive the budget.
        Assert.All(
            restricted,
            region => Assert.True(region.Blocks.Count <= 2));
        Assert.All(
            restricted,
            region => Assert.True(region.Blocks.Count < full.Blocks.Count));
    }

    [Fact]
    public void AndChain_TinySpeculationBudget_RejectsTheChainRegion()
    {
        var method = PrepareMethod(GetSnippet(nameof(Snippets.AndChain)));
        var full = Assert.Single(
            IfConditionConversion.FindRegions(method, 64, 256));
        var restricted = IfConditionConversion.FindRegions(
            method, maxInnerBlocks: 64, maxSpeculatedValues: 0);
        // The chain region speculates its non-entry compares and must be gone;
        // only regions speculating nothing (constant merges) may survive.
        Assert.All(
            restricted,
            region => Assert.True(region.Blocks.Count < full.Blocks.Count));
    }

    [Fact]
    public void ThreeWayChain_IsDetectedWithThreeExits()
    {
        var method = PrepareMethod(CompilationTestBaseAccessor.GetKernel(
            typeof(ShortCircuitKernels),
            nameof(ShortCircuitKernels.ThreeWayChainKernel)));
        var regions = IfConditionConversion.FindRegions(method, 64, 256);

        var region = Assert.Single(regions);
        Assert.Equal(3, region.Exits.Count);
    }

    [Fact]
    public void NestedIf_MinimalLadder_IsNotChurned()
    {
        // NestedIfKernel is already the minimal 2-branch/3-exit ladder — the
        // profitability guard must leave it alone.
        var method = PrepareMethod(CompilationTestBaseAccessor.GetKernel(
            typeof(BasicIfKernels),
            nameof(BasicIfKernels.NestedIfKernel)));
        var regions = IfConditionConversion.FindRegions(method, 64, 256);
        Assert.Empty(regions);
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        _helper.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Small helper mirroring <see cref="CompilationTestBase.GetKernel"/> for
/// non-derived test classes.
/// </summary>
file static class CompilationTestBaseAccessor
{
    public static MethodInfo GetKernel(System.Type kernelClass, string name) =>
        kernelClass.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .AsNotNull();
}
