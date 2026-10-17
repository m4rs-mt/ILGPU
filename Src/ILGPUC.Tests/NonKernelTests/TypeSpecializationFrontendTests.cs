// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeSpecializationFrontendTests.cs
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
using ILGPUC.Tests.Kernels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that <c>typeof(T)</c> tests, type properties and helper predicates fold in
/// the frontend, so that no type value, reflection call or dead arm reaches the IR,
/// and that everything outside the supported subset fails with a named error.
/// </summary>
public sealed class TypeSpecializationFrontendTests
{
    /// <summary>
    /// Kernels that must fail. Defined here, outside <c>Kernels/</c>, so that the
    /// kernel registry does not pick them up.
    /// </summary>
    private static class ErrorKernels
    {
        public static void ReflectionMember<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            data[index] = typeof(T).Name.Length;
        }

        public static void BoxedTypeTest<T>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> data,
            ArrayView1D<int, Stride1D.Dense> result)
            where T : unmanaged
        {
            result[index] = data[index] is int ? 1 : 0;
        }

        public static void RuntimeTypeValue(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Type type = index == 0 ? typeof(int) : typeof(float);
            data[index] = type == typeof(int) ? 1 : 2;
        }

        public static void EnumUnderlyingType(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var type = typeof(DayOfWeek).GetEnumUnderlyingType();
            data[index] = type == typeof(int) ? 1 : 2;
        }

        public static void ThrowInKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            if (data[index] < 0)
                throw new InvalidOperationException();
            data[index] = 1;
        }

        public static void ThrowBeforeOtherError<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            if (data[index] < 0)
                throw new InvalidOperationException();
            data[index] = typeof(T).Name.Length;
        }

        public static void ThrowingAndFailingHelpers<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            data[index] = Throwing<T>(data[index]) + NameLength<T>();
        }

        private static int Throwing<T>(int value)
        {
            if (value < 0)
                throw new InvalidOperationException();
            return value;
        }

        private static int NameLength<T>() => typeof(T).Name.Length;

        public static void BitCastSizeMismatch(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> input,
            ArrayView1D<long, Stride1D.Dense> output)
        {
            output[index] = Unsafe.BitCast<int, long>(input[index]);
        }

        public static void UnevaluatedPredicateGuardsThrow<T>(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
            where T : unmanaged
        {
            if (TooSlow<T>())
                throw new NotSupportedException();
            data[index] = 1;
        }

        private static bool TooSlow<T>()
        {
            int sum = 0;
            for (int i = 0; i < 1_000_000; ++i)
                sum += i;
            return sum == Unsafe.SizeOf<T>();
        }
    }

    /// <summary>
    /// Returns a kernel of <paramref name="type"/>, closed over the given type
    /// arguments.
    /// </summary>
    private static MethodInfo GetKernel(
        Type type,
        string name,
        params Type[] typeArguments)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Kernel '{name}' not found");
        return typeArguments.Length > 0
            ? method.MakeGenericMethod(typeArguments)
            : method;
    }

    /// <summary>
    /// Compiles <paramref name="kernel"/> for the CPU backend and asserts that it
    /// fails with an error whose message chain contains all fragments.
    /// </summary>
    private static Exception AssertCompileError(
        MethodInfo kernel,
        params string[] fragments)
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
        var text = string.Join("\n", messages);
        foreach (var fragment in fragments)
        {
            Assert.True(
                text.Contains(fragment, StringComparison.Ordinal),
                $"Expected '{fragment}' in:\n{text}");
        }
        return exception;
    }

    public static IEnumerable<object[]> FoldedKernels()
    {
        var kernels = new[]
        {
            nameof(TypeSpecializationKernels.TypeEqualitySelect),
            nameof(TypeSpecializationKernels.TypeInequality),
            nameof(TypeSpecializationKernels.TypeEqualsMethod),
            nameof(TypeSpecializationKernels.TypeOrAndConditional),
            nameof(TypeSpecializationKernels.TypeSwitch),
            nameof(TypeSpecializationKernels.TypeLocal),
            nameof(TypeSpecializationKernels.TypeProperties),
            nameof(TypeSpecializationKernels.SizeOfGuard),
            nameof(TypeSpecializationKernels.GetTypeOfValue),
            nameof(TypeSpecializationKernels.NestedPredicates),
            nameof(TypeSpecializationKernels.RecursivePredicate),
        };
        foreach (var kernel in kernels)
        {
            foreach (var type in new[] { typeof(float), typeof(double), typeof(int) })
                yield return [kernel, type];
        }
    }

    [Theory]
    [MemberData(nameof(FoldedKernels))]
    public void TypeTests_FoldInTheFrontend(string kernelName, Type typeArgument)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = GetKernel(
            typeof(TypeSpecializationKernels),
            kernelName,
            typeArgument);

        var ir = helper.GetNormalizedIR(kernel, IRDumpPoint.AfterFrontend);

        Assert.DoesNotContain("handle", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTypeFromHandle", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("op_Equality", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("@IsFloat", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("@IsSame", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("@Depth", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("@CountDown", ir, StringComparison.Ordinal);
        Assert.DoesNotMatch("@SizeOf(?!Guard)", ir);
    }

    [Theory]
    [InlineData(typeof(FloatOperation))]
    [InlineData(typeof(IntOperation))]
    public void StructPredicates_FoldInTheFrontend(Type operation)
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        foreach (var name in new[]
        {
            nameof(TypeOperationKernels.StaticAbstractPredicate),
            nameof(TypeOperationKernels.InstancePredicate),
        })
        {
            var kernel = GetKernel(typeof(TypeOperationKernels), name, operation);
            var ir = helper.GetNormalizedIR(kernel, IRDumpPoint.AfterFrontend);
            Assert.DoesNotContain("@IsFloat", ir, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GenericSin_DeadArmsAreAbsent()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = GetKernel(
            typeof(ThrowingTypeSpecializationKernels),
            nameof(ThrowingTypeSpecializationKernels.GenericSinKernel),
            typeof(float));

        var ir = helper.GetNormalizedIR(kernel, IRDumpPoint.AfterFrontend);

        Assert.Contains("sin", ir, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cos", ir, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotSupportedException", ir, StringComparison.Ordinal);
    }

    [Fact]
    public void DeadLoop_IsRemoved()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = GetKernel(
            typeof(ThrowingTypeSpecializationKernels),
            nameof(ThrowingTypeSpecializationKernels.DeadLoopKernel),
            typeof(double));

        var code = helper.GenerateBackendCode(kernel, BackendType.CPU);

        Assert.False(string.IsNullOrEmpty(code.SourceCode));
    }

    [Fact]
    public void ReflectionMember_IsNamed() =>
        AssertCompileError(
            GetKernel(
                typeof(ErrorKernels),
                nameof(ErrorKernels.ReflectionMember),
                typeof(int)),
            "get_Name",
            "only typeof(T) comparisons");

    [Fact]
    public void BoxedTypeTest_SuggestsTypeof() =>
        AssertCompileError(
            GetKernel(
                typeof(ErrorKernels),
                nameof(ErrorKernels.BoxedTypeTest),
                typeof(int)),
            "Boxing a value of type 'Int32'",
            "compare typeof(T)");

    [Fact]
    public void RuntimeTypeValue_IsRejected() =>
        AssertCompileError(
            GetKernel(typeof(ErrorKernels), nameof(ErrorKernels.RuntimeTypeValue)),
            "op_Equality",
            "computed at run time");

    [Fact]
    public void OtherReflectionMembers_AreRejected() =>
        AssertCompileError(
            GetKernel(typeof(ErrorKernels), nameof(ErrorKernels.EnumUnderlyingType)),
            "GetEnumUnderlyingType",
            "is not supported in kernels");

    [Fact]
    public void ReachableThrow_InKernel_IsNamed()
    {
        var exception = AssertCompileError(
            GetKernel(typeof(ErrorKernels), nameof(ErrorKernels.ThrowInKernel)),
            "Exceptions are not supported in kernels",
            "'InvalidOperationException'",
            "ThrowInKernel");
        Assert.True(ReachableThrowException.IsCausedBy(exception));
    }

    [Theory]
    [InlineData(nameof(ErrorKernels.ThrowBeforeOtherError))]
    [InlineData(nameof(ErrorKernels.ThrowingAndFailingHelpers))]
    public void OtherErrors_TakePrecedenceOverReachableThrows(string kernelName)
    {
        // A throw alone may make an instantiation skippable; another error must
        // always fail the build, whichever comes first
        var exception = AssertCompileError(
            GetKernel(typeof(ErrorKernels), kernelName, typeof(int)),
            "get_Name");
        Assert.False(ReachableThrowException.IsCausedBy(exception));
    }

    [Fact]
    public void ReachableThrow_InHelper_IsNamedWithCaller()
    {
        var exception = AssertCompileError(
            GetKernel(
                typeof(ThrowingTypeSpecializationKernels),
                nameof(ThrowingTypeSpecializationKernels.GenericSinKernel),
                typeof(int)),
            "GenericSin<Int32>",
            "could not be compiled",
            "'NotSupportedException'");
        Assert.True(ReachableThrowException.IsCausedBy(exception));
    }

    [Fact]
    public void BitCastSizeMismatch_IsNamed() =>
        AssertCompileError(
            GetKernel(typeof(ErrorKernels), nameof(ErrorKernels.BitCastSizeMismatch)),
            "Unsafe.BitCast<Int32, Int64>",
            "sizes differ");

    [Fact]
    public void UnevaluatedPredicate_ExplainsWhy() =>
        AssertCompileError(
            GetKernel(
                typeof(ErrorKernels),
                nameof(ErrorKernels.UnevaluatedPredicateGuardsThrow),
                typeof(int)),
            "TooSlow<Int32>",
            "was not evaluated at compile time",
            "budget");
}
