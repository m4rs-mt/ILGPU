// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeTestRegressionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Frontend;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Regression tests for type tests on boxed value types: folded results must match
/// .NET, and what .NET rejects at run time must not compile silently.
/// </summary>
public sealed class TypeTestRegressionTests
{
    /// <summary>
    /// Helpers evaluated against their run-time results.
    /// </summary>
    private static class Helpers
    {
        public static bool IsFloatBoxed<T>() => default(T) is float;

        public static bool SameBox<T>(int x)
        {
            object a = x;
            object b = a;
            return a == b;
        }

        public static bool DistinctBox<T>(int x)
        {
            object a = x;
            object b = x;
            return a == b;
        }

        public static int DefaultLocal<T>()
            where T : struct
        {
            T value = default;
            return value is int i ? i : 7;
        }
    }

    /// <summary>
    /// Kernels that must fold, compile or fail with a named error.
    /// </summary>
    private static class Kernels
    {
        public static void BoxedHelperGuard<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            if (Helpers.IsFloatBoxed<T>())
                data[index] = 1;
            else
                throw new NotSupportedException();
        }

        public static void TwoBoxes<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            ArrayView1D<T, Stride1D.Dense> x)
            where T : unmanaged
        {
            data[index] = (object)x[index] == (object)x[index] ? 11 : 22;
        }

        public static void AsNullable<T>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> x,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            data[index] = (x[index] as int?).GetValueOrDefault(-1);
        }

        public static void UnboxMismatch<T>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> x,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            data[index] = (int)(object)x[index];
        }
    }

    private readonly ConcurrentDictionary<
        (MethodBase, string, bool),
        ConstantEvaluationResult> _memo = new();

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
    /// Returns a kernel or helper closed over <paramref name="type"/>.
    /// </summary>
    private static MethodInfo Get(Type owner, string name, Type type) =>
        owner.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(type);

    /// <summary>
    /// Compiles a kernel and returns the messages of the error it fails with.
    /// </summary>
    private static string CompileError(MethodInfo kernel)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var exception = Assert.ThrowsAny<Exception>(() =>
            helper.GenerateBackendCode(kernel, BackendType.CPU));
        var messages = new List<string>();
        for (var current = exception;
             current is not null;
             current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join("\n", messages);
    }

    [Theory]
    [InlineData(nameof(Helpers.IsFloatBoxed), typeof(float), new object[0])]
    [InlineData(nameof(Helpers.IsFloatBoxed), typeof(int), new object[0])]
    [InlineData(nameof(Helpers.SameBox), typeof(int), new object[] { 3 })]
    [InlineData(nameof(Helpers.DistinctBox), typeof(int), new object[] { 3 })]
    [InlineData(nameof(Helpers.DefaultLocal), typeof(int), new object[0])]
    [InlineData(nameof(Helpers.DefaultLocal), typeof(float), new object[0])]
    public void Evaluation_MatchesRuntime(string name, Type type, object[] arguments)
    {
        var method = Get(typeof(Helpers), name, type);
        var evaluator = new ConstantEvaluator(_memo, Disassemble, 100_000, true);

        var result = evaluator.Evaluate(method, (object?[])arguments.Clone());

        // Not evaluable is fine for the box identity helpers; a constant must
        // be the run-time answer
        if (result.IsConstant || name is nameof(Helpers.IsFloatBoxed) or
            nameof(Helpers.DefaultLocal))
        {
            Assert.True(result.IsConstant, result.Reason);
            Assert.Equal(method.Invoke(null, arguments), result.Value);
        }
    }

    [Fact]
    public void BoxedHelperGuard_FoldsAndCompiles()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = Get(
            typeof(Kernels),
            nameof(Kernels.BoxedHelperGuard),
            typeof(float));

        var code = helper.GenerateBackendCode(kernel, BackendType.CPU);

        Assert.False(string.IsNullOrEmpty(code.SourceCode));
    }

    [Fact]
    public void DistinctBoxes_AreNotEqual()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = Get(typeof(Kernels), nameof(Kernels.TwoBoxes), typeof(int));

        var ir = helper.GetNormalizedIR(kernel, IRDumpPoint.AfterFrontend);

        Assert.Contains("store i32 22", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("store i32 11", ir, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(float))]
    public void NullableTargets_AreRejected(Type type) =>
        Assert.Contains(
            "Int32",
            CompileError(Get(typeof(Kernels), nameof(Kernels.AsNullable), type)),
            StringComparison.Ordinal);

    [Fact]
    public void MismatchedUnbox_IsAnInvalidCast()
    {
        var text = CompileError(
            Get(typeof(Kernels), nameof(Kernels.UnboxMismatch), typeof(float)));

        Assert.Contains("'InvalidCastException'", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchingUnbox_Compiles()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = Get(typeof(Kernels), nameof(Kernels.UnboxMismatch), typeof(int));

        var code = helper.GenerateBackendCode(kernel, BackendType.CPU);

        Assert.False(string.IsNullOrEmpty(code.SourceCode));
    }
}
