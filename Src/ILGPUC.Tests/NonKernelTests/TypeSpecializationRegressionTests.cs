// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeSpecializationRegressionTests.cs
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
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Regression tests for compile-time type specialization: every evaluated result
/// must match what .NET computes at run time, and valid kernels must compile.
/// </summary>
public sealed class TypeSpecializationRegressionTests
{
    /// <summary>
    /// An interface whose default method calls another default method that a
    /// struct overrides.
    /// </summary>
    public interface IDefaultOperation
    {
        bool IsFloat() => false;

        bool Check() => IsFloat();
    }

    /// <summary>
    /// Overrides the inner default method.
    /// </summary>
    public struct FloatDefaultOperation : IDefaultOperation
    {
        public readonly bool IsFloat() => true;
    }

    /// <summary>
    /// Helpers evaluated against their run-time results.
    /// </summary>
    private static class Helpers
    {
        public static int GeZero<T>(int x) => x >= 0 ? 1 : 2;

        public static int LeZero<T>(int x) => x <= 0 ? 1 : 2;

        public static int LtZero<T>(int x)
        {
            if (x < 0)
                return 1;
            return 2;
        }

        public static int GeUnsigned<T>(uint x) => x >= 5u ? 1 : 2;

        public static int GtUnsigned<T>(uint x) => x > 0x80000000u ? 1 : 2;

        public static int NaNOrdering<T>(float x) =>
            (x >= 1.0f ? 1 : 0) + (x < 1.0f ? 2 : 0) + (!(x <= 1.0f) ? 4 : 0);

        public static bool FloatSum<T>(float a, float b) => a + b == 0.3f;

        public static bool IsBig<T>(byte b) => b > 127;

        public static bool DefaultCheck<TOp>()
            where TOp : struct, IDefaultOperation =>
            default(TOp).Check();

        public static Type TypeOf<T>() => typeof(T);
    }

    /// <summary>
    /// Kernels that must compile and fold.
    /// </summary>
    private static class Kernels
    {
        public static void ByteArgument<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            data[index] = Helpers.IsBig<T>(200) ? 11 : 22;
        }

        public static void TypeReturningHelper<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            data[index] = Helpers.TypeOf<T>() == typeof(int) ? 11 : 22;
        }

        public static void HoistedBoolLoop<T>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> data,
            int count)
            where T : unmanaged
        {
            bool isFloat = typeof(T) == typeof(float);
            for (int k = 0; k < count; ++k)
            {
                if (isFloat)
                    data[index] = Unsafe.BitCast<float, T>(1.0f);
                else
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// The try block ends in a throw, directly followed by its handler,
        /// which the frontend entered as if the throw fell through to it.
        /// </summary>
        public static void ThrowingTryInRemovedBranch<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            if (typeof(T) == typeof(int))
            {
                data[index] = 11;
            }
            else
            {
                try
                {
                    throw new NotSupportedException();
                }
                catch (NotSupportedException)
                {
                    data[index] = 22;
                }
            }
        }

        public static void HoistedTypeLoop<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data,
            int count)
            where T : unmanaged
        {
            var type = typeof(T);
            for (int k = 0; k < count; ++k)
                data[index] += type == typeof(float) ? 1 : 2;
        }

        public static void Irreducible<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            int x = data[index];
            if (typeof(T) == typeof(int))
                goto L1;
            goto L2;
        L1:
            x += 1;
        L2:
            x *= 2;
            if (typeof(T) == typeof(double))
                goto L1;
            data[index] = x;
        }

        public static void Irreducible2<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            int x = data[index];
            if (typeof(T) != typeof(int))
                goto L2;
        L1:
            x += 1;
        L2:
            x *= 2;
            if (typeof(T) == typeof(double))
                goto L1;
            data[index] = x;
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
    /// Returns a helper closed over <see cref="int"/>.
    /// </summary>
    private static MethodInfo Helper(string name, Type? typeArgument = null) =>
        typeof(Helpers)
            .GetMethod(name, BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeArgument ?? typeof(int));

    /// <summary>
    /// Evaluates a helper with the operands the frontend would pass and asserts
    /// that the result matches the run-time result.
    /// </summary>
    private void AssertMatchesRuntime(
        MethodInfo method,
        object?[] operands,
        object?[] runtimeArguments)
    {
        var evaluator = new ConstantEvaluator(_memo, Disassemble, 100_000, true);
        var result = evaluator.Evaluate(method, operands);
        Assert.True(result.IsConstant, result.Reason);
        Assert.Equal(method.Invoke(null, runtimeArguments), result.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MinValue)]
    public void SignedComparisons_MatchRuntime(int value)
    {
        foreach (var name in new[] { "GeZero", "LeZero", "LtZero" })
            AssertMatchesRuntime(Helper(name), [value], [value]);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(5u)]
    [InlineData(0x80000000u)]
    [InlineData(0xFFFFFFFFu)]
    public void UnsignedComparisons_MatchRuntime(uint value)
    {
        foreach (var name in new[] { "GeUnsigned", "GtUnsigned" })
            AssertMatchesRuntime(Helper(name), [unchecked((int)value)], [value]);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void FloatOrdering_MatchesRuntime(float value) =>
        AssertMatchesRuntime(Helper("NaNOrdering"), [value], [value]);

    [Fact]
    public void FloatArithmetic_RoundsToSinglePrecision() =>
        AssertMatchesRuntime(Helper("FloatSum"), [0.1f, 0.2f], [0.1f, 0.2f]);

    [Fact]
    public void ByteArguments_AreNarrowedFromSignExtendedConstants() =>
        // The frontend passes IR constants, in which 200 is the Int8 value -56
        AssertMatchesRuntime(Helper("IsBig"), [-56], [(byte)200]);

    [Fact]
    public void DefaultInterfaceMethods_AreNeverResolvedToTheWrongBody()
    {
        var method = Helper("DefaultCheck", typeof(FloatDefaultOperation));
        var evaluator = new ConstantEvaluator(_memo, Disassemble, 100_000, true);

        var result = evaluator.Evaluate(method, []);

        // Not evaluable is fine; a constant must be the run-time answer
        if (result.IsConstant)
            Assert.Equal(method.Invoke(null, null), result.Value);
    }

    [Theory]
    [InlineData(typeof(int), "store i32 11")]
    [InlineData(typeof(float), "store i32 11")]
    public void ByteArgument_FoldsToTheRuntimeResult(Type type, string expected)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(nameof(Kernels.ByteArgument))!
            .MakeGenericMethod(type);

        var ir = helper.GetNormalizedIR(kernel, IRDumpPoint.AfterFrontend);

        Assert.Contains(expected, ir, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(int), "store i32 11")]
    [InlineData(typeof(float), "store i32 22")]
    public void TypeReturningHelpers_AreEvaluated(Type type, string expected)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(nameof(Kernels.TypeReturningHelper))!
            .MakeGenericMethod(type);

        var ir = helper.GetNormalizedIR(kernel, IRDumpPoint.AfterFrontend);

        Assert.Contains(expected, ir, StringComparison.Ordinal);
        Assert.DoesNotContain("@TypeOf", ir, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(Kernels.HoistedBoolLoop), typeof(float))]
    [InlineData(nameof(Kernels.HoistedTypeLoop), typeof(float))]
    [InlineData(nameof(Kernels.HoistedTypeLoop), typeof(int))]
    [InlineData(nameof(Kernels.ThrowingTryInRemovedBranch), typeof(int))]
    public void ValidKernels_Compile(string kernelName, Type type)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(kernelName)!.MakeGenericMethod(type);

        var code = helper.GenerateBackendCode(kernel, BackendType.CPU);

        Assert.False(string.IsNullOrEmpty(code.SourceCode));
    }

    /// <summary>
    /// Type tests in irreducible control flow (a <c>goto</c> into a loop). The
    /// frontend does not fold branches in such methods; the IR passes do.
    /// </summary>
    [Theory]
    [InlineData(nameof(Kernels.Irreducible), typeof(int))]
    [InlineData(nameof(Kernels.Irreducible), typeof(float))]
    [InlineData(nameof(Kernels.Irreducible2), typeof(int))]
    [InlineData(nameof(Kernels.Irreducible2), typeof(float))]
    public void IrreducibleKernels_Compile(string kernelName, Type type) =>
        ValidKernels_Compile(kernelName, type);

    /// <summary>
    /// For double, the type tests keep the loop and remove its only exit: the
    /// kernel never returns, which is a named error.
    /// </summary>
    [Theory]
    [InlineData(nameof(Kernels.Irreducible))]
    [InlineData(nameof(Kernels.Irreducible2))]
    public void IrreducibleKernels_ThatNeverReturn_AreNamedErrors(string kernelName)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(kernelName)!
            .MakeGenericMethod(typeof(double));

        var exception = Assert.ThrowsAny<Exception>(() =>
            helper.GenerateBackendCode(kernel, BackendType.CPU));

        Assert.Contains(
            "never returns",
            exception.InnerException?.Message ?? exception.Message,
            StringComparison.Ordinal);
    }
}
