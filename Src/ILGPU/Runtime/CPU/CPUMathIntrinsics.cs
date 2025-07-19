// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUMathIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// Mathematical operations for vectorized execution.
/// Uses TensorPrimitives for hardware-accelerated SIMD operations.
/// </summary>
public static class CPUMathIntrinsics
{
    #region Trigonometric Functions

    /// <summary>
    /// Computes element-wise sine across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane sine results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Sin<T>(ReadOnlySpan<T> input) where T : ITrigonometricFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Sin<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise cosine across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane cosine results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Cos<T>(ReadOnlySpan<T> input) where T : ITrigonometricFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Cos<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise tangent across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane tangent results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Tan<T>(ReadOnlySpan<T> input) where T : ITrigonometricFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Tan<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise arcsine across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane arcsine results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Asin<T>(ReadOnlySpan<T> input) where T : ITrigonometricFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Asin<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise arccosine across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane arccosine results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Acos<T>(ReadOnlySpan<T> input) where T : ITrigonometricFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Acos<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise arctangent across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane arctangent results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Atan<T>(ReadOnlySpan<T> input) where T : ITrigonometricFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Atan<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise hyperbolic sine across all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Sinh<T>(ReadOnlySpan<T> input) where T : IHyperbolicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Sinh<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise hyperbolic cosine across all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Cosh<T>(ReadOnlySpan<T> input) where T : IHyperbolicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Cosh<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise hyperbolic tangent across all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Tanh<T>(ReadOnlySpan<T> input) where T : IHyperbolicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Tanh<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise inverse hyperbolic sine across all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Asinh<T>(ReadOnlySpan<T> input) where T : IHyperbolicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Asinh<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise inverse hyperbolic cosine across all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Acosh<T>(ReadOnlySpan<T> input) where T : IHyperbolicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Acosh<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise inverse hyperbolic tangent across all lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Atanh<T>(ReadOnlySpan<T> input) where T : IHyperbolicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Atanh<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise two-argument arctangent across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPointIeee754{T}"/>.
    /// </typeparam>
    /// <param name="y">The y values, one per lane.</param>
    /// <param name="x">The x values, one per lane.</param>
    /// <returns>A new array containing per-lane arctangent results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Atan2<T>(ReadOnlySpan<T> y, ReadOnlySpan<T> x)
        where T : IFloatingPointIeee754<T>
    {
        var res = new T[y.Length];
        TensorPrimitives.Atan2<T>(y, x, res);
        return res;
    }

    #endregion

    #region Power and Root Functions

    /// <summary>
    /// Computes element-wise square root across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IRootFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane square root results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Sqrt<T>(ReadOnlySpan<T> input) where T : IRootFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Sqrt<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise reciprocal square root across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPointIeee754{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane reciprocal square root results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Rsqrt<T>(ReadOnlySpan<T> input) where T : IFloatingPointIeee754<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.ReciprocalSqrt<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise power across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IPowerFunctions{T}"/>.
    /// </typeparam>
    /// <param name="x">The base values, one per lane.</param>
    /// <param name="y">The exponent values, one per lane.</param>
    /// <returns>A new array containing per-lane power results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Pow<T>(ReadOnlySpan<T> x, ReadOnlySpan<T> y)
        where T : IPowerFunctions<T>
    {
        var res = new T[x.Length];
        TensorPrimitives.Pow<T>(x, y, res);
        return res;
    }

    #endregion

    #region Exponential and Logarithmic Functions

    /// <summary>
    /// Computes element-wise natural exponential across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IExponentialFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane exponential results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Exp<T>(ReadOnlySpan<T> input) where T : IExponentialFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Exp<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise base-2 exponential across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IExponentialFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane base-2 exponential results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Exp2<T>(ReadOnlySpan<T> input) where T : IExponentialFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Exp2<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise natural logarithm across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ILogarithmicFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane natural logarithm results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Log<T>(ReadOnlySpan<T> input) where T : ILogarithmicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Log<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise base-2 logarithm across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ILogarithmicFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane base-2 logarithm results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Log2<T>(ReadOnlySpan<T> input) where T : ILogarithmicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Log2<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise base-10 logarithm across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ILogarithmicFunctions{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane base-10 logarithm results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Log10<T>(ReadOnlySpan<T> input) where T : ILogarithmicFunctions<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Log10<T>(input, res);
        return res;
    }

    #endregion

    #region Rounding and Absolute Value Functions

    /// <summary>
    /// Computes element-wise absolute value across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="INumberBase{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane absolute value results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Abs<T>(ReadOnlySpan<T> input) where T : INumberBase<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Abs<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise floor across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPoint{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane floor results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Floor<T>(ReadOnlySpan<T> input) where T : IFloatingPoint<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Floor<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise ceiling across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPoint{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane ceiling results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Ceiling<T>(ReadOnlySpan<T> input) where T : IFloatingPoint<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Ceiling<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise truncation toward zero across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPoint{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane truncation results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Truncate<T>(ReadOnlySpan<T> input) where T : IFloatingPoint<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Truncate<T>(input, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise rounding across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPoint{T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane rounded results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Round<T>(ReadOnlySpan<T> input) where T : IFloatingPoint<T>
    {
        var res = new T[input.Length];
        TensorPrimitives.Round<T>(input, res);
        return res;
    }

    #endregion

    #region Min, Max, and Clamp Functions

    /// <summary>
    /// Computes element-wise minimum across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="INumber{T}"/>.
    /// </typeparam>
    /// <param name="a">The first operand values, one per lane.</param>
    /// <param name="b">The second operand values, one per lane.</param>
    /// <returns>A new array containing per-lane minimum results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Min<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b) where T : INumber<T>
    {
        var res = new T[a.Length];
        TensorPrimitives.Min<T>(a, b, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise maximum across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="INumber{T}"/>.
    /// </typeparam>
    /// <param name="a">The first operand values, one per lane.</param>
    /// <param name="b">The second operand values, one per lane.</param>
    /// <returns>A new array containing per-lane maximum results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Max<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b) where T : INumber<T>
    {
        var res = new T[a.Length];
        TensorPrimitives.Max<T>(a, b, res);
        return res;
    }

    /// <summary>
    /// Clamps each lane value to the inclusive range [min, max].
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="INumber{T}"/>.
    /// </typeparam>
    /// <param name="value">The values to clamp, one per lane.</param>
    /// <param name="min">The per-lane minimum bounds.</param>
    /// <param name="max">The per-lane maximum bounds.</param>
    /// <returns>A new array containing per-lane clamped results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Clamp<T>(
        ReadOnlySpan<T> value, ReadOnlySpan<T> min, ReadOnlySpan<T> max)
        where T : INumber<T>
    {
        var res = new T[value.Length];
        TensorPrimitives.Clamp<T>(value, min, max, res);
        return res;
    }

    #endregion

    #region Fused Multiply-Add

    /// <summary>
    /// Computes element-wise fused multiply-add (a * b + c) across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPointIeee754{T}"/>.
    /// </typeparam>
    /// <param name="a">The multiplicand values, one per lane.</param>
    /// <param name="b">The multiplier values, one per lane.</param>
    /// <param name="c">The addend values, one per lane.</param>
    /// <returns>A new array containing per-lane fused multiply-add results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] MultiplyAdd<T>(
        ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c)
        where T : IFloatingPointIeee754<T>
    {
        var res = new T[a.Length];
        TensorPrimitives.FusedMultiplyAdd<T>(a, b, c, res);
        return res;
    }

    #endregion

    #region Binary Arithmetic Operations

    /// <summary>
    /// Computes element-wise addition across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IAdditionOperators{T,T,T}"/>
    /// and <see cref="IAdditiveIdentity{T,T}"/>.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new array containing per-lane addition results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Add<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        var res = new T[left.Length];
        TensorPrimitives.Add<T>(left, right, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise subtraction across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ISubtractionOperators{T,T,T}"/>
    /// and <see cref="IAdditiveIdentity{T,T}"/>.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new array containing per-lane subtraction results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Subtract<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : ISubtractionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        var res = new T[left.Length];
        TensorPrimitives.Subtract<T>(left, right, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise multiplication across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IMultiplyOperators{T,T,T}"/>
    /// and <see cref="IMultiplicativeIdentity{T,T}"/>.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new array containing per-lane multiplication results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Multiply<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
    {
        var res = new T[left.Length];
        TensorPrimitives.Multiply<T>(left, right, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise division across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IDivisionOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="left">The dividend values, one per lane.</param>
    /// <param name="right">The divisor values, one per lane.</param>
    /// <returns>A new array containing per-lane division results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Divide<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IDivisionOperators<T, T, T>
    {
        var res = new T[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] / right[i];
        return res;
    }

    /// <summary>
    /// Computes element-wise remainder across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IModulusOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="left">The dividend values, one per lane.</param>
    /// <param name="right">The divisor values, one per lane.</param>
    /// <returns>A new array containing per-lane remainder results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Remainder<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IModulusOperators<T, T, T>
    {
        var res = new T[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] % right[i];
        return res;
    }

    /// <summary>
    /// Computes element-wise negation across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IUnaryNegationOperators{T,T}"/>.
    /// </typeparam>
    /// <param name="input">The input values, one per lane.</param>
    /// <returns>A new array containing per-lane negation results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Negate<T>(ReadOnlySpan<T> input)
        where T : IUnaryNegationOperators<T, T>
    {
        var res = new T[input.Length];
        for (int i = 0; i < input.Length; i++)
            res[i] = -input[i];
        return res;
    }

    #endregion

    #region Bitwise Operations

    /// <summary>
    /// Computes element-wise bitwise AND across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IBitwiseOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new array containing per-lane bitwise AND results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] BitwiseAnd<T>(
        ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IBitwiseOperators<T, T, T>
    {
        var res = new T[left.Length];
        TensorPrimitives.BitwiseAnd<T>(left, right, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise bitwise OR across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IBitwiseOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new array containing per-lane bitwise OR results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] BitwiseOr<T>(
        ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IBitwiseOperators<T, T, T>
    {
        var res = new T[left.Length];
        TensorPrimitives.BitwiseOr<T>(left, right, res);
        return res;
    }

    /// <summary>
    /// Computes element-wise bitwise XOR across all lanes.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IBitwiseOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new array containing per-lane bitwise XOR results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] Xor<T>(
        ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IBitwiseOperators<T, T, T>
    {
        var res = new T[left.Length];
        TensorPrimitives.Xor<T>(left, right, res);
        return res;
    }

    /// <summary>
    /// Shifts each lane left by the corresponding amount.
    /// </summary>
    /// <param name="left">The values to shift, one per lane.</param>
    /// <param name="right">The per-lane shift amounts.</param>
    /// <returns>A new array containing per-lane left-shift results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int[] ShiftLeft(ReadOnlySpan<int> left, ReadOnlySpan<int> right)
    {
        var res = new int[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] << right[i];
        return res;
    }

    /// <summary>
    /// Shifts each lane right by the corresponding amount.
    /// </summary>
    /// <param name="left">The values to shift, one per lane.</param>
    /// <param name="right">The per-lane shift amounts.</param>
    /// <returns>A new array containing per-lane right-shift results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int[] ShiftRight(ReadOnlySpan<int> left, ReadOnlySpan<int> right)
    {
        var res = new int[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] >> right[i];
        return res;
    }

    /// <summary>
    /// Shifts each lane left by the corresponding amount.
    /// </summary>
    /// <param name="left">The values to shift, one per lane.</param>
    /// <param name="right">The per-lane shift amounts.</param>
    /// <returns>A new array containing per-lane left-shift results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long[] ShiftLeft(ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        var res = new long[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] << (int)right[i];
        return res;
    }

    /// <summary>
    /// Shifts each lane right by the corresponding amount.
    /// </summary>
    /// <param name="left">The values to shift, one per lane.</param>
    /// <param name="right">The per-lane shift amounts.</param>
    /// <returns>A new array containing per-lane right-shift results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long[] ShiftRight(ReadOnlySpan<long> left, ReadOnlySpan<long> right)
    {
        var res = new long[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] >> (int)right[i];
        return res;
    }

    #endregion

    #region Comparison Operations

    /// <summary>
    /// Compares left and right element-wise, returning true per lane where left equals
    /// right.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IEqualityOperators.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new bool array with per-lane comparison results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] Equal<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IEqualityOperators<T, T, bool>
    {
        var res = new bool[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] == right[i];
        return res;
    }

    /// <summary>
    /// Compares left and right element-wise, returning true per lane where left does not
    /// equal right.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IEqualityOperators.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new bool array with per-lane comparison results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] NotEqual<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IEqualityOperators<T, T, bool>
    {
        var res = new bool[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] != right[i];
        return res;
    }

    /// <summary>
    /// Compares left and right element-wise, returning true per lane where left is less
    /// than right.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IComparisonOperators.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new bool array with per-lane comparison results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] LessThan<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IComparisonOperators<T, T, bool>
    {
        var res = new bool[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] < right[i];
        return res;
    }

    /// <summary>
    /// Compares left and right element-wise, returning true per lane where left is less
    /// than or equal to right.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IComparisonOperators.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new bool array with per-lane comparison results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] LessEqual<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IComparisonOperators<T, T, bool>
    {
        var res = new bool[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] <= right[i];
        return res;
    }

    /// <summary>
    /// Compares left and right element-wise, returning true per lane where left is
    /// greater than right.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IComparisonOperators.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new bool array with per-lane comparison results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] GreaterThan<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IComparisonOperators<T, T, bool>
    {
        var res = new bool[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] > right[i];
        return res;
    }

    /// <summary>
    /// Compares left and right element-wise, returning true per lane where left is
    /// greater than or equal to right.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IComparisonOperators.
    /// </typeparam>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    /// <returns>A new bool array with per-lane comparison results.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool[] GreaterEqual<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IComparisonOperators<T, T, bool>
    {
        var res = new bool[left.Length];
        for (int i = 0; i < left.Length; i++)
            res[i] = left[i] >= right[i];
        return res;
    }

    #endregion

    #region In-place Trigonometric Functions

    /// <summary>
    /// Computes element-wise sine across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sin<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ITrigonometricFunctions<T>
        => TensorPrimitives.Sin<T>(input, target);

    /// <summary>
    /// Computes element-wise cosine across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Cos<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ITrigonometricFunctions<T>
        => TensorPrimitives.Cos<T>(input, target);

    /// <summary>
    /// Computes element-wise tangent across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Tan<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ITrigonometricFunctions<T>
        => TensorPrimitives.Tan<T>(input, target);

    /// <summary>
    /// Computes element-wise arcsine across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Asin<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ITrigonometricFunctions<T>
        => TensorPrimitives.Asin<T>(input, target);

    /// <summary>
    /// Computes element-wise arccosine across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Acos<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ITrigonometricFunctions<T>
        => TensorPrimitives.Acos<T>(input, target);

    /// <summary>
    /// Computes element-wise arctangent across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ITrigonometricFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Atan<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ITrigonometricFunctions<T>
        => TensorPrimitives.Atan<T>(input, target);

    /// <summary>Computes element-wise sinh into a pre-allocated target.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sinh<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IHyperbolicFunctions<T>
        => TensorPrimitives.Sinh<T>(input, target);

    /// <summary>Computes element-wise cosh into a pre-allocated target.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Cosh<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IHyperbolicFunctions<T>
        => TensorPrimitives.Cosh<T>(input, target);

    /// <summary>Computes element-wise tanh into a pre-allocated target.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Tanh<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IHyperbolicFunctions<T>
        => TensorPrimitives.Tanh<T>(input, target);

    /// <summary>Computes element-wise asinh into a pre-allocated target.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Asinh<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IHyperbolicFunctions<T>
        => TensorPrimitives.Asinh<T>(input, target);

    /// <summary>Computes element-wise acosh into a pre-allocated target.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Acosh<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IHyperbolicFunctions<T>
        => TensorPrimitives.Acosh<T>(input, target);

    /// <summary>Computes element-wise atanh into a pre-allocated target.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Atanh<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IHyperbolicFunctions<T>
        => TensorPrimitives.Atanh<T>(input, target);

    /// <summary>
    /// Computes element-wise two-argument arctangent across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPointIeee754{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="y">The y-component values, one per lane.</param>
    /// <param name="x">The x-component values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Atan2<T>(Span<T> target, ReadOnlySpan<T> y, ReadOnlySpan<T> x)
        where T : IFloatingPointIeee754<T>
        => TensorPrimitives.Atan2<T>(y, x, target);

    #endregion

    #region In-place Power and Root Functions

    /// <summary>
    /// Computes element-wise square root across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IRootFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sqrt<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IRootFunctions<T>
        => TensorPrimitives.Sqrt<T>(input, target);

    /// <summary>
    /// Computes element-wise reciprocal square root across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPointIeee754{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Rsqrt<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IFloatingPointIeee754<T>
        => TensorPrimitives.ReciprocalSqrt<T>(input, target);

    /// <summary>
    /// Computes element-wise power across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IPowerFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="x">The base values, one per lane.</param>
    /// <param name="y">The exponent values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pow<T>(Span<T> target, ReadOnlySpan<T> x, ReadOnlySpan<T> y)
        where T : IPowerFunctions<T>
        => TensorPrimitives.Pow<T>(x, y, target);

    #endregion

    #region In-place Exponential and Logarithmic Functions

    /// <summary>
    /// Computes element-wise base-e exponential across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IExponentialFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Exp<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IExponentialFunctions<T>
        => TensorPrimitives.Exp<T>(input, target);

    /// <summary>
    /// Computes element-wise base-2 exponential across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IExponentialFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Exp2<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IExponentialFunctions<T>
        => TensorPrimitives.Exp2<T>(input, target);

    /// <summary>
    /// Computes element-wise natural logarithm across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ILogarithmicFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Log<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ILogarithmicFunctions<T>
        => TensorPrimitives.Log<T>(input, target);

    /// <summary>
    /// Computes element-wise base-2 logarithm across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ILogarithmicFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Log2<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ILogarithmicFunctions<T>
        => TensorPrimitives.Log2<T>(input, target);

    /// <summary>
    /// Computes element-wise base-10 logarithm across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ILogarithmicFunctions{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Log10<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : ILogarithmicFunctions<T>
        => TensorPrimitives.Log10<T>(input, target);

    #endregion

    #region In-place Rounding and Absolute Value Functions

    /// <summary>
    /// Computes element-wise absolute value across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="INumberBase{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Abs<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : INumberBase<T>
        => TensorPrimitives.Abs<T>(input, target);

    /// <summary>
    /// Computes element-wise floor across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPoint{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Floor<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IFloatingPoint<T>
        => TensorPrimitives.Floor<T>(input, target);

    /// <summary>
    /// Computes element-wise ceiling across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPoint{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ceiling<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IFloatingPoint<T>
        => TensorPrimitives.Ceiling<T>(input, target);

    /// <summary>
    /// Computes element-wise truncation across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPoint{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Truncate<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IFloatingPoint<T>
        => TensorPrimitives.Truncate<T>(input, target);

    /// <summary>
    /// Computes element-wise rounding across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPoint{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Round<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IFloatingPoint<T>
        => TensorPrimitives.Round<T>(input, target);

    #endregion

    #region In-place Min, Max, and Clamp Functions

    /// <summary>
    /// Computes element-wise minimum across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="INumber{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="a">The first operand values, one per lane.</param>
    /// <param name="b">The second operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Min<T>(Span<T> target, ReadOnlySpan<T> a, ReadOnlySpan<T> b)
        where T : INumber<T>
        => TensorPrimitives.Min<T>(a, b, target);

    /// <summary>
    /// Computes element-wise maximum across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="INumber{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="a">The first operand values, one per lane.</param>
    /// <param name="b">The second operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Max<T>(Span<T> target, ReadOnlySpan<T> a, ReadOnlySpan<T> b)
        where T : INumber<T>
        => TensorPrimitives.Max<T>(a, b, target);

    /// <summary>
    /// Computes element-wise clamp across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="INumber{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="value">The values to clamp, one per lane.</param>
    /// <param name="min">The minimum bound values, one per lane.</param>
    /// <param name="max">The maximum bound values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clamp<T>(
        Span<T> target,
        ReadOnlySpan<T> value,
        ReadOnlySpan<T> min,
        ReadOnlySpan<T> max)
        where T : INumber<T>
        => TensorPrimitives.Clamp<T>(value, min, max, target);

    #endregion

    #region In-place Fused Multiply-Add

    /// <summary>
    /// Computes element-wise fused multiply-add across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IFloatingPointIeee754{T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="a">The first operand values, one per lane.</param>
    /// <param name="b">The second operand values, one per lane.</param>
    /// <param name="c">The addend values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyAdd<T>(
        Span<T> target,
        ReadOnlySpan<T> a,
        ReadOnlySpan<T> b,
        ReadOnlySpan<T> c)
        where T : IFloatingPointIeee754<T>
        => TensorPrimitives.FusedMultiplyAdd<T>(a, b, c, target);

    #endregion

    #region In-place Binary Arithmetic Operations

    /// <summary>
    /// Computes element-wise addition across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IAdditionOperators{T,T,T}"/>
    /// and <see cref="IAdditiveIdentity{T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add<T>(
        Span<T> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
        => TensorPrimitives.Add<T>(left, right, target);

    /// <summary>
    /// Computes element-wise subtraction across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="ISubtractionOperators{T,T,T}"/>
    /// and <see cref="IAdditiveIdentity{T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Subtract<T>(
        Span<T> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : ISubtractionOperators<T, T, T>, IAdditiveIdentity<T, T>
        => TensorPrimitives.Subtract<T>(left, right, target);

    /// <summary>
    /// Computes element-wise multiplication across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IMultiplyOperators{T,T,T}"/>
    /// and <see cref="IMultiplicativeIdentity{T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply<T>(
        Span<T> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
        => TensorPrimitives.Multiply<T>(left, right, target);

    /// <summary>
    /// Computes element-wise division across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IDivisionOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The dividend values, one per lane.</param>
    /// <param name="right">The divisor values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Divide<T>(
        Span<T> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IDivisionOperators<T, T, T>
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] / right[i];
    }

    /// <summary>
    /// Computes element-wise remainder across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IModulusOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The dividend values, one per lane.</param>
    /// <param name="right">The divisor values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Remainder<T>(
        Span<T> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IModulusOperators<T, T, T>
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] % right[i];
    }

    /// <summary>
    /// Computes element-wise negation across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IUnaryNegationOperators{T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Negate<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IUnaryNegationOperators<T, T>
    {
        for (int i = 0; i < input.Length; i++)
            target[i] = -input[i];
    }

    /// <summary>
    /// Computes element-wise bitwise NOT (ones complement) across all lanes
    /// into a pre-allocated target.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnesComplement<T>(Span<T> target, ReadOnlySpan<T> input)
        where T : IBitwiseOperators<T, T, T>
    {
        for (int i = 0; i < input.Length; i++)
            target[i] = ~input[i];
    }

    #endregion

    #region In-place Bitwise Operations

    /// <summary>
    /// Computes element-wise bitwise AND across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IBitwiseOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BitwiseAnd<T>(
        Span<T> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IBitwiseOperators<T, T, T>
        => TensorPrimitives.BitwiseAnd<T>(left, right, target);

    /// <summary>
    /// Computes element-wise bitwise OR across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IBitwiseOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BitwiseOr<T>(
        Span<T> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IBitwiseOperators<T, T, T>
        => TensorPrimitives.BitwiseOr<T>(left, right, target);

    /// <summary>
    /// Computes element-wise bitwise XOR across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying <see cref="IBitwiseOperators{T,T,T}"/>.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Xor<T>(
        Span<T> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IBitwiseOperators<T, T, T>
        => TensorPrimitives.Xor<T>(left, right, target);

    /// <summary>
    /// Computes element-wise left shift (int) across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The values to shift, one per lane.</param>
    /// <param name="right">The shift amounts, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ShiftLeft(
        Span<int> target,
        ReadOnlySpan<int> left,
        ReadOnlySpan<int> right)
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] << right[i];
    }

    /// <summary>
    /// Computes element-wise right shift (int) across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The values to shift, one per lane.</param>
    /// <param name="right">The shift amounts, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ShiftRight(
        Span<int> target,
        ReadOnlySpan<int> left,
        ReadOnlySpan<int> right)
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] >> right[i];
    }

    /// <summary>
    /// Computes element-wise left shift (long) across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The values to shift, one per lane.</param>
    /// <param name="right">The shift amounts, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ShiftLeft(
        Span<long> target,
        ReadOnlySpan<long> left,
        ReadOnlySpan<long> right)
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] << (int)right[i];
    }

    /// <summary>
    /// Computes element-wise right shift (long) across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The values to shift, one per lane.</param>
    /// <param name="right">The shift amounts, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ShiftRight(
        Span<long> target,
        ReadOnlySpan<long> left,
        ReadOnlySpan<long> right)
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] >> (int)right[i];
    }

    #endregion

    #region In-place Comparison Operations

    /// <summary>
    /// Computes element-wise equality across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IEqualityOperators.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Equal<T>(
        Span<bool> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IEqualityOperators<T, T, bool>
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] == right[i];
    }

    /// <summary>
    /// Computes element-wise inequality across all lanes into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IEqualityOperators.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotEqual<T>(
        Span<bool> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IEqualityOperators<T, T, bool>
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] != right[i];
    }

    /// <summary>
    /// Computes element-wise less-than comparison across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IComparisonOperators.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LessThan<T>(
        Span<bool> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IComparisonOperators<T, T, bool>
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] < right[i];
    }

    /// <summary>
    /// Computes element-wise less-or-equal comparison across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IComparisonOperators.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LessEqual<T>(
        Span<bool> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IComparisonOperators<T, T, bool>
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] <= right[i];
    }

    /// <summary>
    /// Computes element-wise greater-than comparison across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IComparisonOperators.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GreaterThan<T>(
        Span<bool> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IComparisonOperators<T, T, bool>
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] > right[i];
    }

    /// <summary>
    /// Computes element-wise greater-or-equal comparison across all lanes
    /// into a pre-allocated target.
    /// </summary>
    /// <typeparam name="T">
    /// The element type satisfying IComparisonOperators.
    /// </typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="left">The left operand values, one per lane.</param>
    /// <param name="right">The right operand values, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GreaterEqual<T>(
        Span<bool> target,
        ReadOnlySpan<T> left,
        ReadOnlySpan<T> right)
        where T : IComparisonOperators<T, T, bool>
    {
        for (int i = 0; i < left.Length; i++)
            target[i] = left[i] >= right[i];
    }

    #endregion

    #region Convert Operations

    /// <summary>
    /// Converts each element from TSource to TTarget, allocating a new array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget[] Convert<TSource, TTarget>(ReadOnlySpan<TSource> input)
        where TSource : INumberBase<TSource>
        where TTarget : INumberBase<TTarget>
    {
        var res = new TTarget[input.Length];
        for (int i = 0; i < input.Length; i++)
            res[i] = TTarget.CreateTruncating(input[i]);
        return res;
    }

    /// <summary>
    /// Converts each element from TSource to TTarget into a pre-allocated target.
    /// </summary>
    /// <typeparam name="TSource">The source numeric type.</typeparam>
    /// <typeparam name="TTarget">The target numeric type.</typeparam>
    /// <param name="target">The destination span to write results into.</param>
    /// <param name="input">The input values to convert, one per lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Convert<TSource, TTarget>(
        Span<TTarget> target,
        ReadOnlySpan<TSource> input)
        where TSource : INumberBase<TSource>
        where TTarget : INumberBase<TTarget>
    {
        for (int i = 0; i < input.Length; i++)
            target[i] = TTarget.CreateTruncating(input[i]);
    }

    #endregion
}
