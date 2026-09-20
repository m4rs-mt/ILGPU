// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConstantEvaluatorTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Frontend;
using ILGPUC.Tests.Kernels;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests the compile-time evaluation of helper predicates: results, memoization,
/// recursion handling, the instruction budget and IL arithmetic semantics.
/// </summary>
public sealed class ConstantEvaluatorTests
{
    private readonly ConcurrentDictionary<
        (MethodBase, string, bool),
        ConstantEvaluationResult> _memo = new();

    /// <summary>
    /// Creates an evaluator with the given budget.
    /// </summary>
    private ConstantEvaluator CreateEvaluator(int budget = 100_000) =>
        new(_memo, Disassemble, budget, is64Bit: true);

    /// <summary>
    /// Disassembles a method, or returns null.
    /// </summary>
    private static DisassembledMethod? Disassemble(MethodBase method)
    {
        try
        {
            return Disassembler.TryDisassemble(method);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns a closed generic method of <see cref="ConstantEvaluatorTests"/> or
    /// <see cref="Predicates"/>.
    /// </summary>
    private static MethodInfo Get(Type type, string name, params Type[] typeArguments)
    {
        var method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        return typeArguments.Length > 0
            ? method.MakeGenericMethod(typeArguments)
            : method;
    }

    [Fact]
    public void NestedPredicates_FoldAtAnyDepth()
    {
        var evaluator = CreateEvaluator();
        var isFloat = Get(typeof(Predicates), nameof(Predicates.IsFloat), typeof(float));
        var isInt = Get(typeof(Predicates), nameof(Predicates.IsFloat), typeof(int));

        var floatResult = evaluator.Evaluate(isFloat, []);
        Assert.True(floatResult.IsConstant, floatResult.Reason);
        Assert.Equal(true, floatResult.Value);

        var intResult = evaluator.Evaluate(isInt, []);
        Assert.True(intResult.IsConstant, intResult.Reason);
        Assert.Equal(false, intResult.Value);
    }

    [Fact]
    public void Memo_IsHitOnRepeatedEvaluation()
    {
        var evaluator = CreateEvaluator();
        var isFloat = Get(typeof(Predicates), nameof(Predicates.IsFloat), typeof(double));

        evaluator.Evaluate(isFloat, []);
        int hitsAfterFirst = evaluator.MemoHitCount;
        var second = evaluator.Evaluate(isFloat, []);

        Assert.True(second.IsConstant, second.Reason);
        Assert.Equal(hitsAfterFirst + 1, evaluator.MemoHitCount);
    }

    [Fact]
    public void RecursionOverDifferentInstantiations_Terminates()
    {
        var evaluator = CreateEvaluator();
        var depth = Get(typeof(Predicates), nameof(Predicates.Depth), typeof(float));

        var result = evaluator.Evaluate(depth, []);

        Assert.True(result.IsConstant, result.Reason);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void RecursionWithSameArguments_IsNotConstantWithReason()
    {
        var evaluator = CreateEvaluator();
        var spin = Get(typeof(ConstantEvaluatorTests), nameof(Spin), typeof(int));

        var result = evaluator.Evaluate(spin, [3]);

        Assert.False(result.IsConstant);
        Assert.Contains("recursively", result.Reason);
    }

    [Fact]
    public void RecursionWithDifferentArguments_Folds()
    {
        var evaluator = CreateEvaluator();
        var countDown = Get(
            typeof(Predicates),
            nameof(Predicates.CountDown),
            typeof(int));

        var result = evaluator.Evaluate(countDown, [3]);

        Assert.True(result.IsConstant, result.Reason);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void RecursionOverValues_Folds()
    {
        // Kernels rely on this when they call a recursive helper with constant
        // arguments (see CodeGenerator.TryEvaluateCall)
        var evaluator = CreateEvaluator();
        var factorial = Get(typeof(ConstantEvaluatorTests), nameof(Factorial));

        var result = evaluator.Evaluate(factorial, [10]);

        Assert.True(result.IsConstant, result.Reason);
        Assert.Equal(3_628_800, result.Value);
    }

    [Fact]
    public void RecursionDeeperThanTheLimit_IsNotConstantAndNotMemoized()
    {
        var evaluator = CreateEvaluator();
        var sumBelow = Get(typeof(ConstantEvaluatorTests), nameof(SumBelow));

        // SumBelow(n) nests n + 1 calls
        var deep = evaluator.Evaluate(sumBelow, [ConstantEvaluator.MaxCallDepth]);
        var shallow = evaluator.Evaluate(
            sumBelow,
            [ConstantEvaluator.MaxCallDepth - 1]);

        Assert.False(deep.IsConstant);
        Assert.True(deep.IsBudgetExceeded);
        Assert.Contains("deep", deep.Reason);
        Assert.True(shallow.IsConstant, shallow.Reason);
        Assert.Equal(
            (ConstantEvaluator.MaxCallDepth - 1) * ConstantEvaluator.MaxCallDepth / 2,
            shallow.Value);
    }

    [Fact]
    public void CallsMethods_IsTrueOnlyForMethodsWithCalls()
    {
        var evaluator = CreateEvaluator();

        Assert.True(evaluator.CallsMethods(
            Get(typeof(ConstantEvaluatorTests), nameof(Factorial))));
        Assert.False(evaluator.CallsMethods(
            Get(typeof(ConstantEvaluatorTests), nameof(Work))));
    }

    [Fact]
    public void LongLoop_ExceedsBudget()
    {
        var evaluator = CreateEvaluator(budget: 10_000);
        var loop = Get(typeof(ConstantEvaluatorTests), nameof(LongLoop));

        var result = evaluator.Evaluate(loop, []);

        Assert.False(result.IsConstant);
        Assert.True(result.IsBudgetExceeded);
        Assert.Contains("budget", result.Reason);
    }

    [Fact]
    public void BudgetFailures_AreNotMemoized()
    {
        var loop = Get(typeof(ConstantEvaluatorTests), nameof(LongLoop));

        CreateEvaluator(budget: 10_000).Evaluate(loop, []);
        var result = CreateEvaluator(budget: 50_000_000).Evaluate(loop, []);

        Assert.True(result.IsConstant, result.Reason);
        Assert.Equal(1_000_000, result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MemoizedCalls_ChargeTheirCost(bool evaluateCalleesFirst)
    {
        var first = Get(typeof(ConstantEvaluatorTests), nameof(WorkA), typeof(int));
        var second = Get(typeof(ConstantEvaluatorTests), nameof(WorkB), typeof(int));
        var both = Get(typeof(ConstantEvaluatorTests), nameof(Both), typeof(int));

        // Measure the cost of one callee with a memo of its own, and choose a
        // budget that each callee fits on its own and both together do not
        var cost = new ConstantEvaluator(new(), Disassemble, 1_000_000, is64Bit: true)
            .Evaluate(first, [])
            .Cost;
        int budget = cost * 3 / 2;
        if (evaluateCalleesFirst)
        {
            Assert.True(CreateEvaluator(budget).Evaluate(first, []).IsConstant);
            Assert.True(CreateEvaluator(budget).Evaluate(second, []).IsConstant);
        }

        var result = CreateEvaluator(budget).Evaluate(both, []);

        Assert.False(result.IsConstant);
        Assert.True(result.IsBudgetExceeded);
    }

    [Fact]
    public void Arithmetic_FollowsILSemantics()
    {
        var evaluator = CreateEvaluator();
        var arithmetic = Get(typeof(ConstantEvaluatorTests), nameof(Arithmetic));

        var result = evaluator.Evaluate(arithmetic, [-7]);

        Assert.True(result.IsConstant, result.Reason);
        Assert.Equal(Arithmetic(-7), result.Value);
    }

    [Fact]
    public void Conversions_FollowILSemantics()
    {
        var evaluator = CreateEvaluator();
        var conversions = Get(typeof(ConstantEvaluatorTests), nameof(Conversions));

        foreach (int value in new[] { -1, 0, 7, int.MinValue, int.MaxValue })
        {
            var result = evaluator.Evaluate(conversions, [value]);
            Assert.True(result.IsConstant, result.Reason);
            Assert.Equal(Conversions(value), result.Value);
        }
    }

    [Fact]
    public void FloatArithmetic_FollowsILSemantics()
    {
        var evaluator = CreateEvaluator();
        var floats = Get(typeof(ConstantEvaluatorTests), nameof(FloatArithmetic));

        var result = evaluator.Evaluate(floats, [3.25]);

        Assert.True(result.IsConstant, result.Reason);
        Assert.Equal(FloatArithmetic(3.25f), result.Value);
    }

    [Fact]
    public void CheckedOverflow_IsNotConstant()
    {
        var evaluator = CreateEvaluator();
        var overflow = Get(typeof(ConstantEvaluatorTests), nameof(CheckedAdd));

        var result = evaluator.Evaluate(overflow, [int.MaxValue]);

        Assert.False(result.IsConstant);
        Assert.Contains("overflows", result.Reason);
    }

    [Fact]
    public void ReadingMemory_IsNotConstantWithLocation()
    {
        var evaluator = CreateEvaluator();
        var readsArray = Get(typeof(ConstantEvaluatorTests), nameof(ReadsStaticArray));

        var result = evaluator.Evaluate(readsArray, []);

        Assert.False(result.IsConstant);
        Assert.Contains("IL_", result.Reason);
    }

    [Fact]
    public void TypeArguments_AndReflectionMembers_Fold()
    {
        var evaluator = CreateEvaluator();
        var isSame = Get(typeof(Predicates), nameof(Predicates.IsSame));
        var properties = Get(
            typeof(ConstantEvaluatorTests),
            nameof(TypeProperties),
            typeof(DayOfWeek));

        var same = evaluator.Evaluate(isSame, [typeof(int), typeof(int)]);
        var different = evaluator.Evaluate(isSame, [typeof(int), typeof(uint)]);
        var flags = evaluator.Evaluate(properties, []);

        Assert.Equal(true, same.Value);
        Assert.Equal(false, different.Value);
        Assert.True(flags.IsConstant, flags.Reason);
        Assert.Equal(TypeProperties<DayOfWeek>(), flags.Value);
    }

    [Fact]
    public void StaticReadonlyFields_AndSwitches_Fold()
    {
        var evaluator = CreateEvaluator();
        var classify = Get(
            typeof(ConstantEvaluatorTests),
            nameof(Classify),
            typeof(double));

        var result = evaluator.Evaluate(classify, []);

        Assert.True(result.IsConstant, result.Reason);
        Assert.Equal(Classify<double>(), result.Value);
    }

    // Methods evaluated by the tests

    private static int Spin<T>(int value) => value == 0 ? 0 : Spin<T>(value);

    private static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);

    private static int SumBelow(int n) => n <= 0 ? 0 : n + SumBelow(n - 1);

    private static int Work(int count)
    {
        int sum = 0;
        for (int i = 0; i < count; ++i)
            sum += i;
        return sum;
    }

    private static int WorkA<T>() => Work(2_000);

    private static int WorkB<T>() => Work(2_001);

    private static int Both<T>() => WorkA<T>() + WorkB<T>();

    private static int LongLoop()
    {
        int sum = 0;
        for (int i = 0; i < 1_000_000; ++i)
            sum++;
        return sum;
    }

    private static readonly int[] StaticArray = [1, 2, 3];

    private static int ReadsStaticArray() => StaticArray[1];

    private static int CheckedAdd(int value) => checked(value + 1);

    private static long Arithmetic(int value)
    {
        uint unsignedValue = (uint)value;
        long result = value / 2 + value % 3;
        result += unsignedValue / 3 + unsignedValue % 5;
        result ^= value >> 1;
        result += (int)(unsignedValue >> 1);
        result -= value << 3;
        result *= -value;
        return result + ~value;
    }

    private static long Conversions(int value)
    {
        long result = (byte)value + (sbyte)value + (short)value + (ushort)value;
        result += (long)(ulong)(uint)value;
        result += (long)(uint)value;
        result += (long)value;
        result += (long)(double)(uint)value;
        return result;
    }

    private static float FloatArithmetic(float value)
    {
        float result = value * 3.5f - 1.25f;
        result /= 2.0f;
        result += 1.0f;
        return result + (int)(value * 10.0f) + (value < 4.0f ? 1.0f : 0.0f);
    }

    private static int TypeProperties<T>()
    {
        int result = 0;
        if (typeof(T).IsValueType)
            result |= 1;
        if (typeof(T).IsEnum)
            result |= 2;
        if (typeof(T).IsPrimitive)
            result |= 4;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            result |= 8;
        if (typeof(IComparable).IsAssignableFrom(typeof(T)))
            result |= 16;
        return result + Unsafe.SizeOf<T>() * 32;
    }

    private static int Classify<T>()
    {
        int result = Ops<T>.IsFloatField ? 10 : 20;
        switch (Unsafe.SizeOf<T>())
        {
            case 1: return result + 1;
            case 2: return result + 2;
            case 4: return result + 4;
            case 8: return result + 8;
            default: return result;
        }
    }
}
