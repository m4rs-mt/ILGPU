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
using System.Numerics;
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
    private const ushort ExponentMask = 0x7C00;
    private const ushort MantissaMask = 0x03FF;
    private const ushort MagnitudeMask = 0x7FFF;

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

    /// <summary>
    /// Returns true if the sign bit of the given value is clear.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, if the value is positive, including +0 and positive NaNs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPositive(Half value) =>
        (Interop.FloatAsInt(value) & SignMask) == 0;

    /// <summary>
    /// Returns true if the given value is positive infinity.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, if the value is positive infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPositiveInfinity(Half value) =>
        Interop.FloatAsInt(value) == 0x7C00;

    /// <summary>
    /// Returns true if the given value is negative infinity.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, if the value is negative infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNegativeInfinity(Half value) =>
        Interop.FloatAsInt(value) == 0xFC00;

    /// <summary>
    /// Returns true if the given value is a normal number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    /// True, if the value is neither zero, subnormal, infinite nor NaN.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNormal(Half value)
    {
        int exponent = Interop.FloatAsInt(value) & ExponentMask;
        return exponent != 0 & exponent != ExponentMask;
    }

    /// <summary>
    /// Returns true if the given value is a subnormal number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, if the value is subnormal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSubnormal(Half value)
    {
        int bits = Interop.FloatAsInt(value);
        return (bits & ExponentMask) == 0 & (bits & MantissaMask) != 0;
    }

    /// <summary>
    /// Returns true if the given value is an even integer.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, if the value is an even integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsEvenInteger(Half value) =>
        XMath.IsInteger(value) & XMath.Rem((float)value, 2.0f) == 0.0f;

    /// <summary>
    /// Returns true if the given value is +0 or -0.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, if the value is zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsZero(Half value) =>
        (Interop.FloatAsInt(value) & MagnitudeMask) == 0;

    /// <summary>
    /// Returns true, as every half-precision value is canonical.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsCanonical(Half value) => true;

    /// <summary>
    /// Returns false, as no half-precision value is a complex number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsComplexNumber(Half value) => false;

    /// <summary>
    /// Returns false, as no half-precision value is an imaginary number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsImaginaryNumber(Half value) => false;

    /// <summary>
    /// Returns true if the given value is a real number, which is every value but NaN.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, if the value is not NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsRealNumber(Half value) => !XMath.IsNaN(value);

    /// <summary>
    /// Returns true if the given value is a positive power of two.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    /// True, if the value is positive and either a normal value with an empty
    /// mantissa or a subnormal value with a single mantissa bit.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPow2(Half value)
    {
        int bits = Interop.FloatAsInt(value);
        int exponent = bits & ExponentMask;
        int mantissa = bits & MantissaMask;
        bool normal = exponent != 0 & exponent != ExponentMask & mantissa == 0;
        bool subnormal = exponent == 0 & XMath.PopCount(mantissa) == 1;
        return (bits & SignMask) == 0 & (normal | subnormal);
    }

    #endregion

    #region Comparison

    /// <summary>
    /// Compares two values, ordering NaN below every other value.
    /// </summary>
    /// <param name="self">The first value.</param>
    /// <param name="other">The second value.</param>
    /// <returns>
    /// -1, 0 or 1 if the first value is less than, equal to or greater than the
    /// second value; NaN is less than every other value and equal to NaN.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CompareTo(ref Half self, Half other)
    {
        float x = (float)self;
        float y = (float)other;
        bool xNaN = XMath.IsNaN(x);
        bool yNaN = XMath.IsNaN(y);
        int result = Utilities.Select(x < y, -1, Utilities.Select(x > y, 1, 0));
        return Utilities.Select(
            xNaN,
            Utilities.Select(yNaN, 0, -1),
            Utilities.Select(yNaN, 1, result));
    }

    /// <summary>
    /// Returns true if two values are equal, treating NaN as equal to NaN.
    /// </summary>
    /// <param name="self">The first value.</param>
    /// <param name="other">The second value.</param>
    /// <returns>True, if the values are equal or both NaN.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsEqual(ref Half self, Half other)
    {
        float x = (float)self;
        float y = (float)other;
        return x == y | (XMath.IsNaN(x) & XMath.IsNaN(y));
    }

    /// <summary>
    /// Returns the hash code of a value, which is equal for equal values.
    /// </summary>
    /// <param name="self">The value.</param>
    /// <returns>
    /// The bits of the value, 0 for both zeros and the exponent mask for every NaN.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int HashCode(ref Half self)
    {
        int bits = Interop.FloatAsInt(self);
        bool nanOrZero = XMath.IsNaN(self) | (bits & MagnitudeMask) == 0;
        return Utilities.Select(nanOrZero, bits & ExponentMask, bits);
    }

    /// <summary>
    /// Returns the number of bytes of the exponent of a value, 1.
    /// </summary>
    /// <param name="self">The value.</param>
    /// <returns>1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetExponentByteCount(ref Half self) => 1;

    /// <summary>
    /// Returns the number of bits of the shortest two's complement representation
    /// of the exponent of a value.
    /// </summary>
    /// <param name="self">The value.</param>
    /// <returns>The length of the exponent in bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetExponentShortestBitLength(ref Half self)
    {
        int exponent = ((Interop.FloatAsInt(self) & ExponentMask) >> 10) - 15;
        bool positive = exponent >= 0;
        int bits = Utilities.Select(positive, exponent, ~exponent);
        return Utilities.Select(positive, 32, 33) - XMath.LeadingZeroCount(bits);
    }

    /// <summary>
    /// Returns the number of bits of the significand of a value, 11.
    /// </summary>
    /// <param name="self">The value.</param>
    /// <returns>11.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetSignificandBitLength(ref Half self) => 11;

    /// <summary>
    /// Returns the number of bytes of the significand of a value, 2.
    /// </summary>
    /// <param name="self">The value.</param>
    /// <returns>2.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetSignificandByteCount(ref Half self) => 2;

    #endregion

    #region Bits

    /// <summary>
    /// Computes the bitwise and of the bits of two values.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The value with the bits of left &amp; right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half BitwiseAnd(Half left, Half right) =>
        Interop.IntAsFloat(
            (ushort)(Interop.FloatAsInt(left) & Interop.FloatAsInt(right)));

    /// <summary>
    /// Computes the bitwise or of the bits of two values.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The value with the bits of left | right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half BitwiseOr(Half left, Half right) =>
        Interop.IntAsFloat(
            (ushort)(Interop.FloatAsInt(left) | Interop.FloatAsInt(right)));

    /// <summary>
    /// Computes the bitwise exclusive or of the bits of two values.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The value with the bits of left ^ right.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half ExclusiveOr(Half left, Half right) =>
        Interop.IntAsFloat(
            (ushort)(Interop.FloatAsInt(left) ^ Interop.FloatAsInt(right)));

    /// <summary>
    /// Computes the bitwise complement of the bits of a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value with the bits of ~value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half OnesComplement(Half value) =>
        Interop.IntAsFloat((ushort)~Interop.FloatAsInt(value));

    /// <summary>
    /// Returns the smallest value that compares greater than the given value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The next value towards positive infinity; NaN and positive infinity are
    /// returned unchanged, and -0 gives the smallest positive subnormal value.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half BitIncrement(Half value)
    {
        int bits = Interop.FloatAsInt(value);
        int next = Utilities.Select((bits & SignMask) != 0, bits - 1, bits + 1);
        next = Utilities.Select(bits == SignMask, 0x0001, next);
        bool special = (bits & ExponentMask) == ExponentMask;
        next = Utilities.Select(
            special,
            Utilities.Select(bits == 0xFC00, 0xFBFF, bits),
            next);
        return Interop.IntAsFloat((ushort)next);
    }

    /// <summary>
    /// Returns the largest value that compares less than the given value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The next value towards negative infinity; NaN and negative infinity are
    /// returned unchanged, and +0 gives the smallest negative subnormal value.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half BitDecrement(Half value)
    {
        int bits = Interop.FloatAsInt(value);
        int next = Utilities.Select((bits & SignMask) != 0, bits + 1, bits - 1);
        next = Utilities.Select(bits == 0, 0x8001, next);
        bool special = (bits & ExponentMask) == ExponentMask;
        next = Utilities.Select(
            special,
            Utilities.Select(bits == ExponentMask, 0x7BFF, bits),
            next);
        return Interop.IntAsFloat((ushort)next);
    }

    /// <summary>
    /// Returns the base-2 exponent of the given value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The unbiased exponent of the value, int.MinValue for zero and int.MaxValue
    /// for infinity and NaN, as computed by <see cref="Half.ILogB(Half)"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ILogB(Half value)
    {
        int bits = Interop.FloatAsInt(value) & MagnitudeMask;
        int exponent = bits >> 10;

        // System.Half derives the exponent of a subnormal value from the leading
        // zeros of its significand as a 32-bit integer, -9 - lzcnt(m)
        int subnormal = -9 - XMath.LeadingZeroCount(bits);
        int result = Utilities.Select(exponent == 0, subnormal, exponent - 15);
        result = Utilities.Select(bits == 0, int.MinValue, result);
        return Utilities.Select(exponent == 0x1F, int.MaxValue, result);
    }

    /// <summary>
    /// Computes value * 2^n.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="n">The power of two.</param>
    /// <returns>value * 2^n, rounded once to half precision.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half ScaleB(Half value, int n)
    {
        // Scaling a nonzero finite value by more than 2^40 overflows, and by less
        // than 2^-40 rounds to zero, so the product with the clamped power is exact
        // in single precision and rounds once
        int exponent = XMath.Clamp(n, -48, 48) + 127;
        float scale = Interop.IntAsFloat((uint)exponent << 23);
        return (Half)((float)value * scale);
    }

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

    #region Pi Functions

    /// <summary>
    /// Computes acos(value) / pi.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>acos(value) / pi.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half AcosPi(Half value) =>
        (Half)(XMath.Acos((float)value) / MathF.PI);

    /// <summary>
    /// Computes asin(value) / pi.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>asin(value) / pi.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half AsinPi(Half value) =>
        (Half)(XMath.Asin((float)value) / MathF.PI);

    /// <summary>
    /// Computes atan(value) / pi.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>atan(value) / pi.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half AtanPi(Half value)
    {
        // atan is odd, which keeps the sign of zero
        float x = (float)value;
        return (Half)XMath.CopySign(XMath.Atan(x) / MathF.PI, x);
    }

    /// <summary>
    /// Computes atan2(y, x) / pi.
    /// </summary>
    /// <param name="y">The y coordinate.</param>
    /// <param name="x">The x coordinate.</param>
    /// <returns>atan2(y, x) / pi.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half Atan2Pi(Half y, Half x) =>
        (Half)(XMath.Atan2((float)y, (float)x) / MathF.PI);

    /// <summary>
    /// Splits |x| into its integral part and its fraction, and computes the angle
    /// of the fraction relative to the nearest quarter period, which keeps the
    /// argument of the trigonometric function small and exact.
    /// </summary>
    /// <param name="ax">The absolute value.</param>
    /// <param name="odd">True, if the integral part is odd.</param>
    /// <param name="fraction">The fractional part.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SplitPi(float ax, out bool odd, out float fraction)
    {
        float integral = XMath.Truncate(ax);
        odd = XMath.Rem(integral, 2.0f) == 1.0f;
        fraction = ax - integral;
    }

    /// <summary>
    /// Returns the reduced angle (in multiples of pi) of a fraction in [0, 1]: the
    /// distance to the nearest of 0, 1/2 and 1.
    /// </summary>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The reduced angle in radians.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReducePi(float fraction) =>
        Utilities.Select(
            fraction <= 0.25f,
            fraction,
            Utilities.Select(
                fraction <= 0.75f,
                XMath.Abs(fraction - 0.5f),
                1.0f - fraction)) * MathF.PI;

    /// <summary>
    /// Computes sin(value * pi) exactly at the integers and half integers.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>sin(value * pi).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half SinPi(Half value) => (Half)SinPi((float)value);

    /// <summary>
    /// Computes sin(x * pi) for a half-precision value x.
    /// </summary>
    private static float SinPi(float x)
    {
        float ax = XMath.Abs(x);
        SplitPi(ax, out bool odd, out float fraction);
        float angle = ReducePi(fraction);
        bool nearHalf = fraction > 0.25f & fraction <= 0.75f;
        float sign = Utilities.Select(x > 0.0f, 1.0f, -1.0f) *
            Utilities.Select(odd, -1.0f, 1.0f);
        float large = sign * Utilities.Select(
            nearHalf,
            XMath.Cos(angle),
            XMath.Sin(angle));
        large = Utilities.Select(fraction == 0.0f, x * 0.0f, large);
        large = Utilities.Select(fraction == 0.5f, sign, large);

        // Small values
        float px = x * MathF.PI;
        float small = Utilities.Select(
            ax >= 0.0078125f,
            XMath.Sin(px),
            Utilities.Select(
                ax >= 0.00012207031f,
                px - px * px * px * 0.16666667f,
                px));
        return Utilities.Select(ax > 0.25f, large, small);
    }

    /// <summary>
    /// Computes cos(value * pi) exactly at the integers and half integers.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>cos(value * pi).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half CosPi(Half value) => (Half)CosPi((float)value);

    /// <summary>
    /// Computes cos(x * pi) for a half-precision value x.
    /// </summary>
    private static float CosPi(float x)
    {
        float ax = XMath.Abs(x);
        SplitPi(ax, out bool odd, out float fraction);
        float angle = ReducePi(fraction);
        bool nearHalf = fraction > 0.25f & fraction <= 0.75f;
        float sign = Utilities.Select(odd, -1.0f, 1.0f) *
            Utilities.Select(fraction > 0.5f, -1.0f, 1.0f);
        float large = sign * Utilities.Select(
            nearHalf,
            XMath.Sin(angle),
            XMath.Cos(angle));
        large = Utilities.Select(fraction == 0.5f, 0.0f, large);

        // Small values
        float px = x * MathF.PI;
        float small = Utilities.Select(
            ax >= 0.0078125f,
            XMath.Cos(px),
            Utilities.Select(
                ax >= 0.00012207031f,
                1.0f - px * px * 0.5f,
                1.0f));
        float result = Utilities.Select(ax > 0.25f, large, small);
        return Utilities.Select(XMath.IsFinite(x), result, float.NaN);
    }

    /// <summary>
    /// Computes sin(value * pi) and cos(value * pi).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The sine and the cosine of value * pi.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Half SinPi, Half CosPi) SinCosPi(Half value) =>
        ((Half)SinPi((float)value), (Half)CosPi((float)value));

    /// <summary>
    /// Computes tan(value * pi) exactly at the integers and half integers.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>tan(value * pi).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Half TanPi(Half value)
    {
        float x = (float)value;
        float ax = XMath.Abs(x);
        SplitPi(ax, out bool odd, out float fraction);
        float angle = ReducePi(fraction);
        bool nearHalf = fraction > 0.25f & fraction <= 0.75f;
        float sign = Utilities.Select(x > 0.0f, 1.0f, -1.0f) *
            Utilities.Select(fraction > 0.5f, -1.0f, 1.0f);
        float tan = XMath.Tan(angle);
        float large = sign * Utilities.Select(nearHalf, 1.0f / tan, tan);
        float sign0 = Utilities.Select(x > 0.0f, 1.0f, -1.0f);
        large = Utilities.Select(
            fraction == 0.0f,
            sign0 * Utilities.Select(odd, -0.0f, 0.0f),
            large);
        large = Utilities.Select(
            fraction == 0.5f,
            sign0 * Utilities.Select(
                odd,
                float.NegativeInfinity,
                float.PositiveInfinity),
            large);

        // Small values
        float px = x * MathF.PI;
        float small = Utilities.Select(
            ax >= 0.0078125f,
            XMath.Tan(px),
            Utilities.Select(
                ax >= 0.00012207031f,
                px + px * px * px * 0.33333334f,
                px));
        return (Half)Utilities.Select(ax > 0.25f, large, small);
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Returns true if values of type <typeparamref name="T"/> convert to and from
    /// half precision in kernels: the floating-point types, <see cref="char"/> and
    /// the integer types up to 64 bits. Evaluated at compile time.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <returns>True, if the type is supported.</returns>
    private static bool IsConvertible<T>() =>
        typeof(T) == typeof(Half) || typeof(T) == typeof(float) ||
        typeof(T) == typeof(double) || typeof(T) == typeof(sbyte) ||
        typeof(T) == typeof(byte) || typeof(T) == typeof(short) ||
        typeof(T) == typeof(ushort) || typeof(T) == typeof(char) ||
        typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
        typeof(T) == typeof(long) || typeof(T) == typeof(ulong);

    /// <summary>
    /// Converts a value of a primitive number type to half precision, like the
    /// conversion operators: integers and wider floating-point values round to the
    /// nearest half-precision value, and values beyond its range are infinite.
    /// </summary>
    /// <typeparam name="TOther">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">The converted value.</param>
    /// <returns>
    /// True, if <typeparamref name="TOther"/> is supported (see
    /// <see cref="IsConvertible{T}"/>).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFrom<TOther>(TOther value, out Half result)
    {
        if (typeof(TOther) == typeof(Half))
            result = Unsafe.As<TOther, Half>(ref value);
        else if (typeof(TOther) == typeof(float))
            result = (Half)Unsafe.As<TOther, float>(ref value);
        else if (typeof(TOther) == typeof(double))
            result = (Half)Unsafe.As<TOther, double>(ref value);
        else if (typeof(TOther) == typeof(sbyte))
            result = (Half)Unsafe.As<TOther, sbyte>(ref value);
        else if (typeof(TOther) == typeof(byte))
            result = (Half)Unsafe.As<TOther, byte>(ref value);
        else if (typeof(TOther) == typeof(short))
            result = (Half)Unsafe.As<TOther, short>(ref value);
        else if (typeof(TOther) == typeof(ushort))
            result = (Half)Unsafe.As<TOther, ushort>(ref value);
        else if (typeof(TOther) == typeof(char))
            result = (Half)Unsafe.As<TOther, char>(ref value);
        else if (typeof(TOther) == typeof(int))
            result = (Half)Unsafe.As<TOther, int>(ref value);
        else if (typeof(TOther) == typeof(uint))
            result = (Half)Unsafe.As<TOther, uint>(ref value);
        else if (typeof(TOther) == typeof(long))
            result = (Half)Unsafe.As<TOther, long>(ref value);
        else if (typeof(TOther) == typeof(ulong))
            result = (Half)Unsafe.As<TOther, ulong>(ref value);
        else
        {
            result = default;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Converts a half-precision value to a primitive number type. Conversions to
    /// integers truncate towards zero and saturate: NaN converts to 0, and values
    /// beyond the range of the integer type to its bounds.
    /// </summary>
    /// <typeparam name="TOther">The target type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">The converted value.</param>
    /// <returns>
    /// True, if <typeparamref name="TOther"/> is supported (see
    /// <see cref="IsConvertible{T}"/>).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertTo<TOther>(Half value, out TOther result)
    {
        result = default!;
        float f = (float)value;
        if (typeof(TOther) == typeof(Half))
            Unsafe.As<TOther, Half>(ref result) = value;
        else if (typeof(TOther) == typeof(float))
            Unsafe.As<TOther, float>(ref result) = f;
        else if (typeof(TOther) == typeof(double))
            Unsafe.As<TOther, double>(ref result) = (double)value;
        else if (typeof(TOther) == typeof(sbyte))
            Unsafe.As<TOther, sbyte>(ref result) =
                (sbyte)ToInt64(f, sbyte.MinValue, sbyte.MaxValue);
        else if (typeof(TOther) == typeof(byte))
            Unsafe.As<TOther, byte>(ref result) = (byte)ToUInt64(f, byte.MaxValue);
        else if (typeof(TOther) == typeof(short))
            Unsafe.As<TOther, short>(ref result) =
                (short)ToInt64(f, short.MinValue, short.MaxValue);
        else if (typeof(TOther) == typeof(ushort))
            Unsafe.As<TOther, ushort>(ref result) = (ushort)ToUInt64(f, ushort.MaxValue);
        else if (typeof(TOther) == typeof(char))
            Unsafe.As<TOther, char>(ref result) = (char)ToUInt64(f, char.MaxValue);
        else if (typeof(TOther) == typeof(int))
            Unsafe.As<TOther, int>(ref result) =
                (int)ToInt64(f, int.MinValue, int.MaxValue);
        else if (typeof(TOther) == typeof(uint))
            Unsafe.As<TOther, uint>(ref result) = (uint)ToUInt64(f, uint.MaxValue);
        else if (typeof(TOther) == typeof(long))
            Unsafe.As<TOther, long>(ref result) =
                ToInt64(f, long.MinValue, long.MaxValue);
        else if (typeof(TOther) == typeof(ulong))
            Unsafe.As<TOther, ulong>(ref result) = ToUInt64(f, ulong.MaxValue);
        else
            return false;
        return true;
    }

    /// <summary>
    /// Converts a single-precision value of a half-precision value to a signed
    /// integer, truncating and saturating to the given bounds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ToInt64(float value, long min, long max)
    {
        // Every finite half-precision value fits a long
        float finite = XMath.Clamp(
            Utilities.Select(XMath.IsNaN(value), 0.0f, value),
            -65504.0f,
            65504.0f);
        long result = XMath.Clamp((long)finite, min, max);
        result = Utilities.Select(value == float.PositiveInfinity, max, result);
        return Utilities.Select(value == float.NegativeInfinity, min, result);
    }

    /// <summary>
    /// Converts a single-precision value of a half-precision value to an unsigned
    /// integer, truncating and saturating to the given upper bound.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ToUInt64(float value, ulong max)
    {
        float finite = XMath.Clamp(
            Utilities.Select(XMath.IsNaN(value), 0.0f, value),
            0.0f,
            65504.0f);
        ulong result = XMath.Min((ulong)finite, max);
        return Utilities.Select(value == float.PositiveInfinity, max, result);
    }

    /// <summary>
    /// Creates a half-precision value from a value of another number type.
    /// </summary>
    /// <typeparam name="TOther">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TOther"/> is not supported (see
    /// <see cref="IsConvertible{T}"/>); in a kernel, this is a compile error.
    /// </exception>
    internal static Half Create<TOther>(TOther value)
        where TOther : INumberBase<TOther>
    {
        if (!IsConvertible<TOther>())
            throw new NotSupportedException();
        TryConvertFrom(value, out var result);
        return result;
    }

    /// <summary>
    /// Converts a half-precision value to an integer type, saturating.
    /// </summary>
    /// <typeparam name="TInteger">The integer type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TInteger"/> is not supported (see
    /// <see cref="IsConvertible{T}"/>); in a kernel, this is a compile error.
    /// </exception>
    internal static TInteger ConvertToInteger<TInteger>(Half value)
        where TInteger : IBinaryInteger<TInteger>
    {
        if (!IsConvertible<TInteger>())
            throw new NotSupportedException();
        TryConvertTo(value, out TInteger result);
        return result;
    }

    /// <summary>
    /// Tries to convert a value of another number type to half precision.
    /// </summary>
    /// <typeparam name="TOther">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">The converted value.</param>
    /// <returns>True, if the type is supported.</returns>
    internal static bool TryConvertFromAny<TOther>(TOther value, out Half result)
        where TOther : INumberBase<TOther> =>
        TryConvertFrom(value, out result);

    /// <summary>
    /// Tries to convert a half-precision value to another number type. The checked
    /// conversions saturate like the others, as kernels have no overflow checks.
    /// </summary>
    /// <typeparam name="TOther">The target type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">The converted value.</param>
    /// <returns>True, if the type is supported.</returns>
    internal static bool TryConvertToAny<TOther>(Half value, out TOther result)
        where TOther : INumberBase<TOther> =>
        TryConvertTo(value, out result);

    #endregion
}
