// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeSpecializationKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Generic kernels that select code with <c>typeof(T)</c> tests and type
/// properties, which the frontend evaluates at compile time per instantiation.
/// Every kernel handles every type argument, so all instantiations compile.
/// The test assembly is built in Debug in CI, so the Debug IL shapes (type locals,
/// values merged on the stack) are covered as well.
/// </summary>
static class TypeSpecializationKernels
{
    public static void TypeEqualitySelect<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data)
        where T : unmanaged
    {
        data[index] = Scale(data[index]);
    }

    public static void TypeInequality<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        data[index] = typeof(T) != typeof(int) ? 1 : 2;
    }

    public static void TypeEqualsMethod<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        data[index] = typeof(T).Equals(typeof(float)) ? 1 : 2;
    }

    public static void TypeOrAndConditional<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        bool isFloat = typeof(T) == typeof(float) || typeof(T) == typeof(double);
        bool isInt = typeof(T) == typeof(int) && typeof(T) != typeof(uint);
        data[index] = isFloat ? 1 : isInt ? 2 : 3;
    }

    public static void TypeSwitch<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        data[index] = typeof(T) switch
        {
            var t when t == typeof(float) => 1,
            var t when t == typeof(double) => 2,
            var t when t == typeof(int) => 3,
            _ => 4,
        };
    }

    public static void TypeLocal<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        Type type = typeof(T);
        Type floatType = typeof(float);
        data[index] = type == floatType ? 1 : 2;
    }

    public static void TypeProperties<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        int result = 0;
        if (typeof(T).IsValueType)
            result |= 1;
        if (typeof(T).IsPrimitive)
            result |= 2;
        if (typeof(T).IsEnum)
            result |= 4;
        if (typeof(T).IsGenericType)
            result |= 8;
        if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            result |= 16;
        if (typeof(T).IsAssignableTo(typeof(IConvertible)))
            result |= 32;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            result |= 64;
        data[index] = result;
    }

    public static void GetTypeOfValue<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged
    {
        result[index] = data[index].GetType() == typeof(int) ? 1 : 2;
    }

    public static void SizeOfGuard<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        data[index] = Unsafe.SizeOf<T>() == 4 ? 4 : Unsafe.SizeOf<T>() == 8 ? 8 : 0;
    }

    public static void NestedPredicates<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        int result = 0;
        if (Predicates.IsFloat<T>())
            result |= 1;
        if (Ops<T>.IsFloat())
            result |= 2;
        if (Predicates.IsSame(typeof(T), typeof(int)))
            result |= 4;
        if (Predicates.Depth<T>() > 1)
            result |= 8;
        if (Ops<T>.IsFloatField)
            result |= 16;
        data[index] = result;
    }

    public static void RecursivePredicate<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where T : unmanaged
    {
        // Recursion over different arguments terminates through the memo
        data[index] = Predicates.CountDown<T>(3) == 0 ? 1 : 2;
    }

    /// <summary>
    /// Scales a value with the arithmetic of its actual type.
    /// </summary>
    private static T Scale<T>(T value) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
            return Unsafe.BitCast<float, T>(Unsafe.BitCast<T, float>(value) * 2.0f);
        if (typeof(T) == typeof(double))
            return Unsafe.BitCast<double, T>(Unsafe.BitCast<T, double>(value) * 3.0);
        if (typeof(T) == typeof(int))
            return Unsafe.BitCast<int, T>(Unsafe.BitCast<T, int>(value) + 1);
        return value;
    }
}

/// <summary>
/// Generic kernels that test the type of a value with patterns, which box the
/// value: <c>x is float f</c>, <c>default(T) is int</c>, the null test Roslyn
/// emits for <c>x.Equals(default)</c>, and <c>GetType()</c> on a boxed value.
/// A boxed value type is never null and has an exact type, so all of them fold.
/// </summary>
static class TypeTestKernels
{
    public static void PatternMatch<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> result)
        where T : unmanaged
    {
        var value = data[index];
        if (value is float f)
            result[index] = f * 2.0f;
        else if (value is int i)
            result[index] = i + 0.5f;
        else
            result[index] = -1.0f;
    }

    public static void DefaultIs<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged
    {
        result[index] = default(T) is float ? 1 : default(T) is int ? 2 : 3;
    }

    public static void EquatableDefault<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged, IEquatable<T>
    {
        result[index] = data[index].Equals(default) ? 1 : 2;
    }

    public static void BoxedGetType<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged
    {
        result[index] = ((object)data[index]).GetType() == typeof(float) ? 1 : 2;
    }
}

/// <summary>
/// Kernels that dispatch on a type-operation struct, through static abstract
/// interface members and through struct instance members.
/// </summary>
static class TypeOperationKernels
{
    public static void StaticAbstractPredicate<TOp>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where TOp : struct, ITypeOperation
    {
        data[index] = TOp.IsFloatOperation() ? 1 : 2;
    }

    public static void InstancePredicate<TOp>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
        where TOp : struct, ITypeOperation
    {
        data[index] = default(TOp).IsFloatInstance() ? 1 : 2;
    }
}

/// <summary>
/// A type operation with static abstract and instance predicates.
/// </summary>
public interface ITypeOperation
{
    static abstract bool IsFloatOperation();

    bool IsFloatInstance();
}

/// <summary>
/// The float operation.
/// </summary>
public readonly struct FloatOperation : ITypeOperation
{
    public static bool IsFloatOperation() => Predicates.IsFloat<float>();

    public bool IsFloatInstance() => true;
}

/// <summary>
/// The integer operation.
/// </summary>
public readonly struct IntOperation : ITypeOperation
{
    public static bool IsFloatOperation() => Predicates.IsFloat<int>();

    public bool IsFloatInstance() => false;
}

/// <summary>
/// Helper predicates over type arguments, evaluated at compile time at any depth.
/// </summary>
static class Predicates
{
    public static bool IsFloat<T>() => IsScalar<T>() && !IsInteger<T>();

    public static bool IsScalar<T>() =>
        typeof(T) == typeof(float) ||
        typeof(T) == typeof(double) ||
        IsInteger<T>();

    public static bool IsInteger<T>() =>
        typeof(T) == typeof(int) || typeof(T) == typeof(long);

    public static bool IsSame(Type left, Type right) => left == right;

    /// <summary>
    /// Recursion over different instantiations, which terminates.
    /// </summary>
    public static int Depth<T>() =>
        typeof(T) == typeof(int) ? 1 : 1 + Depth<int>();

    /// <summary>
    /// Recursion with the same instantiation and decreasing arguments.
    /// </summary>
    public static int CountDown<T>(int value) =>
        value <= 0 ? 0 : CountDown<T>(value - 1);
}

/// <summary>
/// Predicates as static members of a generic type.
/// </summary>
static class Ops<T>
{
    public static readonly bool IsFloatField =
        typeof(T) == typeof(float) || typeof(T) == typeof(double);

    public static bool IsFloat() => Predicates.IsFloat<T>();
}

/// <summary>
/// Kernels whose helper throws for type arguments it does not support. They are
/// only instantiated with supported type arguments, so the throw is always in a
/// branch that is removed at compile time.
/// </summary>
static class ThrowingTypeSpecializationKernels
{
    public static void GenericSinKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> x,
        ArrayView1D<T, Stride1D.Dense> ans)
        where T : unmanaged
    {
        ans[index] = GenericSin(x[index]);
    }

    public static void DeadLoopKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data)
        where T : unmanaged
    {
        data[index] = WithDeadLoop(data[index], index);
    }

    /// <summary>
    /// The pattern from issue #1427.
    /// </summary>
    private static T GenericSin<T>(T x) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
            return Unsafe.BitCast<float, T>(MathF.Sin(Unsafe.BitCast<T, float>(x)));
        else if (typeof(T) == typeof(double))
            return Unsafe.BitCast<double, T>(Math.Cos(Unsafe.BitCast<T, double>(x)));
        else
            throw new NotSupportedException();
    }

    /// <summary>
    /// A dead branch containing a loop with a throw, which merges back into live
    /// code. Only forward reachability removes the loop.
    /// </summary>
    private static T WithDeadLoop<T>(T value, int count) where T : unmanaged
    {
        if (typeof(T) != typeof(float) && typeof(T) != typeof(double))
        {
            for (int i = 0; i < count; ++i)
            {
                if (i > 3)
                    throw new InvalidOperationException();
            }
        }
        return value;
    }
}
