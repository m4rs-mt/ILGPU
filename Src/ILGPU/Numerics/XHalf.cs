// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: XHalf.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Numerics;

/// <summary>
/// ILGPU-side reimplementations of <see cref="Half"/> entry points that have no
/// <see cref="XMath"/> counterpart. The constants are built from their IEEE 754
/// binary16 bit patterns, and the functions compute in single precision and round
/// the result, which is how <see cref="Half"/> implements them. They avoid
/// short-circuit operators, which would introduce divergent branches.
/// </summary>
internal static class XHalf
{
    private const ushort SignMask = 0x8000;

    #region Constants

    /// <summary>
    /// Returns 0.
    /// </summary>
    internal static Half Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x0000);
    }

    /// <summary>
    /// Returns -0.
    /// </summary>
    internal static Half NegativeZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x8000);
    }

    /// <summary>
    /// Returns 1.
    /// </summary>
    internal static Half One
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x3C00);
    }

    /// <summary>
    /// Returns -1.
    /// </summary>
    internal static Half NegativeOne
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0xBC00);
    }

    /// <summary>
    /// Returns the smallest positive value (a subnormal).
    /// </summary>
    internal static Half Epsilon
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x0001);
    }

    /// <summary>
    /// Returns the largest finite value.
    /// </summary>
    internal static Half MaxValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x7BFF);
    }

    /// <summary>
    /// Returns the smallest finite value.
    /// </summary>
    internal static Half MinValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0xFBFF);
    }

    /// <summary>
    /// Returns NaN.
    /// </summary>
    internal static Half NaN
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0xFE00);
    }

    /// <summary>
    /// Returns positive infinity.
    /// </summary>
    internal static Half PositiveInfinity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x7C00);
    }

    /// <summary>
    /// Returns negative infinity.
    /// </summary>
    internal static Half NegativeInfinity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0xFC00);
    }

    /// <summary>
    /// Returns the half-precision value of e.
    /// </summary>
    internal static Half E
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x4170);
    }

    /// <summary>
    /// Returns the half-precision value of pi.
    /// </summary>
    internal static Half Pi
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x4248);
    }

    /// <summary>
    /// Returns the half-precision value of tau (2 pi).
    /// </summary>
    internal static Half Tau
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0x4648);
    }

    /// <summary>
    /// Returns the value whose bits are all set (a negative NaN).
    /// </summary>
    internal static Half AllBitsSet
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interop.IntAsFloat((ushort)0xFFFF);
    }

    /// <summary>
    /// Returns the radix of the representation, 2.
    /// </summary>
    internal static int Radix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 2;
    }

    #endregion

    #region Classification

    /// <summary>
    /// Returns true if the sign bit of the given value is set.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, if the value is negative, including -0 and negative NaNs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNegative(Half value) =>
        (Interop.FloatAsInt(value) & SignMask) != 0;

    #endregion

    #region Min/Max

    /// <summary>
    /// Returns the larger value, propagating NaN and ordering -0 below +0.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The larger value, or NaN if either value is NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Max(Half x, Half y)
    {
        float fx = (float)x;
        float fy = (float)y;
        bool takeX = fx > fy | XMath.IsNaN(fx) | (fx == fy & IsNegative(y));
        return Utilities.Select(takeX, x, y);
    }

    /// <summary>
    /// Returns the smaller value, propagating NaN and ordering -0 below +0.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The smaller value, or NaN if either value is NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Min(Half x, Half y)
    {
        float fx = (float)x;
        float fy = (float)y;
        bool takeX = fx < fy | XMath.IsNaN(fx) | (fx == fy & IsNegative(x));
        return Utilities.Select(takeX, x, y);
    }

    /// <summary>
    /// Clamps a value to the given bounds, propagating NaN.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    /// <returns>Min(Max(value, min), max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Clamp(Half value, Half min, Half max) =>
        Min(Max(value, min), max);

    /// <summary>
    /// Returns the larger value as determined by the comparison x &gt; y.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>x if x &gt; y, otherwise y.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half MaxNative(Half x, Half y) => Utilities.Select(x > y, x, y);

    /// <summary>
    /// Returns the smaller value as determined by the comparison x &lt; y.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>x if x &lt; y, otherwise y.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half MinNative(Half x, Half y) => Utilities.Select(x < y, x, y);

    /// <summary>
    /// Clamps a value to the given bounds with the native comparisons.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="min">The lower bound.</param>
    /// <param name="max">The upper bound.</param>
    /// <returns>MinNative(MaxNative(value, min), max).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half ClampNative(Half value, Half min, Half max) =>
        MinNative(MaxNative(value, min), max);

    /// <summary>
    /// Returns the value with the larger magnitude, preferring the positive value
    /// if both magnitudes are equal.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>
    /// The value with the larger magnitude, or NaN if either value is NaN.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half MaxMagnitude(Half x, Half y)
    {
        float ax = XMath.Abs((float)x);
        float ay = XMath.Abs((float)y);
        bool takeX = ax > ay | XMath.IsNaN((float)x) |
            (ax == ay & !IsNegative(x));
        return Utilities.Select(takeX, x, y);
    }

    /// <summary>
    /// Returns the value with the smaller magnitude, preferring the negative value
    /// if both magnitudes are equal.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>
    /// The value with the smaller magnitude, or NaN if either value is NaN.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half MinMagnitude(Half x, Half y)
    {
        float ax = XMath.Abs((float)x);
        float ay = XMath.Abs((float)y);
        bool takeX = ax < ay | XMath.IsNaN((float)x) |
            (ax == ay & IsNegative(x));
        return Utilities.Select(takeX, x, y);
    }

    /// <summary>
    /// Returns the value with the larger magnitude, preferring a number to NaN and
    /// the positive value if both magnitudes are equal.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The value with the larger magnitude.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half MaxMagnitudeNumber(Half x, Half y)
    {
        float ax = XMath.Abs((float)x);
        float ay = XMath.Abs((float)y);
        bool takeX = ax > ay | XMath.IsNaN(ay) | (ax == ay & !IsNegative(x));
        return Utilities.Select(takeX, x, y);
    }

    /// <summary>
    /// Returns the value with the smaller magnitude, preferring a number to NaN and
    /// the negative value if both magnitudes are equal.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The value with the smaller magnitude.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half MinMagnitudeNumber(Half x, Half y)
    {
        float ax = XMath.Abs((float)x);
        float ay = XMath.Abs((float)y);
        bool takeX = ax < ay | XMath.IsNaN(ay) | (ax == ay & IsNegative(x));
        return Utilities.Select(takeX, x, y);
    }

    /// <summary>
    /// Returns the larger value, preferring a number to NaN and ordering -0 below +0.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The larger value, or the other value if one is NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half MaxNumber(Half x, Half y) =>
        Utilities.Select(
            XMath.IsNaN(y),
            x,
            Utilities.Select(XMath.IsNaN(x), y, Max(x, y)));

    /// <summary>
    /// Returns the smaller value, preferring a number to NaN and ordering -0 below +0.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The smaller value, or the other value if one is NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half MinNumber(Half x, Half y) =>
        Utilities.Select(
            XMath.IsNaN(y),
            x,
            Utilities.Select(XMath.IsNaN(x), y, Min(x, y)));

    #endregion

    #region Functions

    /// <summary>
    /// Computes 10^value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>10^value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Exp10(Half value) =>
        (Half)XMath.Pow(10.0f, (float)value);

    /// <summary>
    /// Computes e^value - 1.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>e^value - 1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half ExpM1(Half value) =>
        (Half)(XMath.Exp((float)value) - 1.0f);

    /// <summary>
    /// Computes 2^value - 1.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>2^value - 1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Exp2M1(Half value) =>
        (Half)(XMath.Pow(2.0f, (float)value) - 1.0f);

    /// <summary>
    /// Computes 10^value - 1.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>10^value - 1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Exp10M1(Half value) =>
        (Half)(XMath.Pow(10.0f, (float)value) - 1.0f);

    /// <summary>
    /// Computes the natural logarithm of value + 1.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log(value + 1).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half LogP1(Half value) =>
        (Half)XMath.Log((float)value + 1.0f);

    /// <summary>
    /// Computes the base-2 logarithm of value + 1.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log2(value + 1).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Log2P1(Half value) =>
        (Half)XMath.Log2((float)value + 1.0f);

    /// <summary>
    /// Computes the base-10 logarithm of value + 1.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log10(value + 1).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Log10P1(Half value) =>
        (Half)XMath.Log10((float)value + 1.0f);

    /// <summary>
    /// Computes the n-th root of a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="n">The degree of the root.</param>
    /// <returns>
    /// The n-th root, with the sign of the value for odd n; even roots of negative
    /// values and the 0-th root are NaN.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half RootN(Half value, int n)
    {
        float x = (float)value;
        float ax = XMath.Abs(x);
        bool oddN = (n & 1) != 0;
        float root = Utilities.Select(
            n == 2,
            XMath.Sqrt(ax),
            Utilities.Select(
                n == 3,
                XMath.Cbrt(ax),
                XMath.Pow(ax, 1.0f / n)));

        // Odd roots keep the sign, even roots of zero are +0 or +infinity, and even
        // roots of negative values are NaN
        float result = Utilities.Select(oddN, XMath.CopySign(root, x), root);
        result = Utilities.Select(x < 0.0f & !oddN, float.NaN, result);
        return (Half)Utilities.Select(n == 0, float.NaN, result);
    }

    /// <summary>
    /// Computes the hypotenuse sqrt(x * x + y * y).
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>
    /// sqrt(x * x + y * y), and positive infinity if either value is infinite,
    /// even if the other one is NaN.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Hypot(Half x, Half y)
    {
        float fx = (float)x;
        float fy = (float)y;
        float result = XMath.Sqrt(fx * fx + fy * fy);
        return (Half)Utilities.Select(
            XMath.IsInfinity(fx) | XMath.IsInfinity(fy),
            float.PositiveInfinity,
            result);
    }

    /// <summary>
    /// Interpolates linearly between two values.
    /// </summary>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <param name="amount">The interpolation weight.</param>
    /// <returns>value1 * (1 - amount) + value2 * amount.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Lerp(Half value1, Half value2, Half amount)
    {
        float t = (float)amount;
        return (Half)((float)value1 * (1.0f - t) + (float)value2 * t);
    }

    #endregion
}
