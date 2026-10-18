// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IfConversionIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Framework;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Unit-level tests of <see cref="IfConversion"/>: methods are compiled through
/// the frontend and the pre-conditional O1 pipeline prefix (DebugSetter + basic
/// optimizations), and the pass is applied alone, so that no other pass folds
/// the diamonds first.
/// </summary>
public sealed class IfConversionIRTests : DisposeBase
{
    private readonly CompilationHelper _helper = new();

    #region Snippets

    private static class Snippets
    {
        public static int DivMinusOne(int a, int b) => b > 0 ? a / -1 : a;

        public static int RemMinusOne(int a, int b) => b > 0 ? a % -1 : a;

        public static long DivMinusOneLong(long a, int b) => b > 0 ? a / -1L : a;

        public static long RemMinusOneLong(long a, int b) => b > 0 ? a % -1L : a;

        public static int DivTwo(int a, int b) => b > 0 ? a / 2 : a;

        public static uint DivAllOnesUnsigned(uint a, int b) =>
            b > 0 ? a / uint.MaxValue : a;

        public static int ShuffleInArm(int value) =>
            value > 0 ? Warp.Shuffle(value, 1) : value;

        public static int AllReduceInArm(int value) =>
            value > 0 ? Warp.AllReduce(value, (a, b) => a + b) : value;

        public static int InclusiveScanInArm(int value) =>
            value > 0 ? Warp.InclusiveScan(value, (a, b) => a + b) : value;

        public static int GroupBroadcastInArm(int value) =>
            value > 0 ? Group.Broadcast(value, 0) : value;

        public static int NestedDiamond(int a)
        {
            int result;
            if (a > 0)
            {
                int inner = a > 10 ? a + 1 : a + 2;
                result = inner * 3;
            }
            else
            {
                result = a - 1;
            }
            return result;
        }

        public static int SequentialDiamonds(int a, int b)
        {
            int first = a / b > 0 ? a + 1 : a + 2;
            return first > 5 ? first + 3 : first - 3;
        }

        public static int NestedDivisionArgument(int a, int b)
        {
            int result;
            if (b != 0)
            {
                int quotient = a > 3 ? a / b : 0;
                result = quotient + 1;
            }
            else
            {
                result = 1;
            }
            return result;
        }

        public static int NestedDivisionCondition(int a, int b)
        {
            int result;
            if (b != 0)
            {
                int inner = a / b > 2 ? 5 : 7;
                result = inner + 1;
            }
            else
            {
                result = 1;
            }
            return result;
        }
    }

    #endregion

    #region Helpers

    private static MethodInfo GetSnippet(string name) =>
        typeof(Snippets).GetMethod(
            name, BindingFlags.Public | BindingFlags.Static).AsNotNull();

    /// <summary>
    /// Compiles the method, runs the O1 pipeline prefix that precedes
    /// AddConditionalOptimizations (DebugSetter + basic optimizations in
    /// Release mode) followed by <see cref="IfConversion"/> alone, and returns
    /// the entry-point method of the result.
    /// </summary>
    private Method ApplyIfConversion(MethodInfo method)
    {
        var props = new CompilationProperties(
            OptimizationLevel: OptimizationLevel.O1)
            .WithMode(CompilationMode.Release);
        var module = _helper.CompileToModule(method, props);

        var builder = Transformer.CreateBuilder();
        builder.AddDebugSetter();
        builder.AddBasicOptimizations();
        builder.Add(static args => new IfConversion(args));
        return builder
            .ToTransformer()
            .Apply(props, _helper.TypeManager, module)
            .EntryPoint;
    }

    /// <summary>
    /// Returns the number of conditional branches of the given method.
    /// </summary>
    private static int CountBranches(Method method)
    {
        int count = 0;
        foreach (var block in method.Blocks)
        {
            if (block.TerminationKind == BlockTerminationKind.Conditional)
                ++count;
        }
        return count;
    }

    #endregion

    #region Tests

    [Theory]
    [InlineData(nameof(Snippets.DivMinusOne))]
    [InlineData(nameof(Snippets.RemMinusOne))]
    [InlineData(nameof(Snippets.DivMinusOneLong))]
    [InlineData(nameof(Snippets.RemMinusOneLong))]
    public void DivisionByMinusOne_KeepsTheBranch(string snippet)
    {
        // A signed division by -1 overflows for the minimum value, so it must
        // stay behind its guard like a division by zero.
        var method = ApplyIfConversion(GetSnippet(snippet));
        Assert.Equal(1, CountBranches(method));
    }

    [Theory]
    [InlineData(nameof(Snippets.DivTwo))]
    [InlineData(nameof(Snippets.DivAllOnesUnsigned))]
    public void DivisionByNonFaultingConstant_BecomesPredicate(string snippet)
    {
        // Neither a signed division by another constant nor an unsigned one by
        // all ones can fault
        var method = ApplyIfConversion(GetSnippet(snippet));
        Assert.Equal(0, CountBranches(method));
    }

    [Theory]
    [InlineData(nameof(Snippets.ShuffleInArm))]
    [InlineData(nameof(Snippets.AllReduceInArm))]
    [InlineData(nameof(Snippets.InclusiveScanInArm))]
    [InlineData(nameof(Snippets.GroupBroadcastInArm))]
    public void CollectiveInArm_KeepsTheBranch(string snippet)
    {
        // Only the threads taking a branch may execute its collectives, which
        // determine the threads a reduction or scan combines
        var method = ApplyIfConversion(GetSnippet(snippet));
        Assert.Equal(1, CountBranches(method));
    }

    [Fact]
    public void NestedDiamonds_BecomePredicates()
    {
        var method = ApplyIfConversion(GetSnippet(nameof(Snippets.NestedDiamond)));
        Assert.Equal(0, CountBranches(method));
    }

    [Fact]
    public void SequentialDiamonds_BecomePredicates()
    {
        // The second diamond reads the phi of the first one, whose condition
        // divides before either branch, so both become predicates
        var method = ApplyIfConversion(
            GetSnippet(nameof(Snippets.SequentialDiamonds)));
        Assert.Equal(0, CountBranches(method));
    }

    [Fact]
    public void NestedDivisionArgument_KeepsTheBranches()
    {
        // The inner diamond keeps its branch to guard the division, so the
        // predicates of the outer one would read its phi, which becomes dead
        var method = ApplyIfConversion(
            GetSnippet(nameof(Snippets.NestedDivisionArgument)));
        Assert.Equal(2, CountBranches(method));
    }

    [Fact]
    public void NestedDivisionCondition_KeepsTheOuterBranch()
    {
        // The inner condition divides by the value the outer branch checks for
        // zero: the inner diamond becomes a predicate, the outer one may not
        var method = ApplyIfConversion(
            GetSnippet(nameof(Snippets.NestedDivisionCondition)));
        Assert.Equal(1, CountBranches(method));
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        _helper.Dispose();
        base.Dispose(disposing);
    }
}
