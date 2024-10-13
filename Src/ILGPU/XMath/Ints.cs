// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Ints.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU;

partial class XMath
{
    /// <summary>
    /// Realizes an integer division of <paramref name="numerator"/>
    /// divided by <paramref name="denominator"/> while rounding the result
    /// down to zero.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <returns>The numerator divided by the denominator rounded to zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DivRoundDown(int numerator, int denominator) =>
        numerator / denominator;

    /// <summary>
    /// Realizes an integer division of <paramref name="numerator"/>
    /// divided by <paramref name="denominator"/> while rounding the result
    /// up (away from zero).
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <returns>
    /// The numerator divided by the denominator rounded up (away from zero).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DivRoundUp(int numerator, int denominator) =>
        (numerator + denominator - 1) / denominator;

    /// <summary>
    /// Realizes an integer division of <paramref name="numerator"/>
    /// divided by <paramref name="denominator"/> while rounding the result
    /// down to zero.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <returns>The numerator divided by the denominator rounded to zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long DivRoundDown(long numerator, long denominator) =>
        numerator / denominator;

    /// <summary>
    /// Realizes an integer division of <paramref name="numerator"/>
    /// divided by <paramref name="denominator"/> while rounding the result
    /// up (away from zero).
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    /// <returns>
    /// The numerator divided by the denominator rounded up (away from zero).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long DivRoundUp(long numerator, long denominator) =>
        (numerator + denominator - 1L) / denominator;

    /// <summary>
    /// Pads the numerator to the next bigger multiple of the denominator.
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="denominator">The denominator.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long PadToMultiple(long numerator, long denominator) =>
        DivRoundUp(numerator, denominator) * denominator;

    /// <summary>
    /// Tests if a floating point value is an integer
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True, if the value is an integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInteger(double value)
    {
        var remainder = Rem(Abs(value), 2.0);
        return remainder < 1e-9 | remainder >= 1.0 - 1e-9;
    }

    /// <summary>
    /// Tests if a floating point value is an integer
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True, if the value is an integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInteger(float value)
    {
        var remainder = Rem(Abs(value), 2.0f);
        return remainder < 1e-6f | remainder >= 1.0f - 1e-6f;
    }

    /// <summary>
    /// Tests if a floating point value is an odd integer
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True, if the value is an odd integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOddInteger(double value)
    {
        var remainder = Rem(Abs(value), 2.0);
        return remainder >= 1.0 - 1e-9;
    }

    /// <summary>
    /// Tests if a floating point value is an odd integer
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True, if the value is an odd integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOddInteger(float value)
    {
        var remainder = Rem(Abs(value), 2.0f);
        return remainder >= 1.0f - 1e-6f;
    }

    /// <summary>
    /// Returns true if the given integer is a power of two.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>True, if the given integer is a power of two.</returns>
    public static bool IsPowerOf2(int value) =>
        value != int.MinValue & IsPowerOf2((uint)Abs(value));

    /// <summary>
    /// Returns true if the given integer is a power of two.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>True, if the given integer is a power of two.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPowerOf2(uint value) =>
        value > 0 & ((value & (value - 1)) == 0);

    /// <summary>
    /// Returns true if the given integer is a power of two.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>True, if the given integer is a power of two.</returns>
    public static bool IsPowerOf2(long value) =>
        value != long.MinValue & IsPowerOf2((ulong)Abs(value));

    /// <summary>
    /// Returns true if the given integer is a power of two.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>True, if the given integer is a power of two.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPowerOf2(ulong value) =>
        value > 0 & ((value & (value - 1)) == 0);

    /// <summary>
    /// Computes the greatest common divisor using the Euclidean algorithm.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The GCD of both numbers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GCD(long a, long b)
    {
        while (b > 0)
        {
            long remainder = a % b;
            a = b;
            b = remainder;
        }
        return a;
    }

    /// <summary>
    /// Computes the least common multiple.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The LCM of both numbers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long LCM(long a, long b) =>
        a * b / GCD(a, b);

    /// <summary>
    /// Represents integer parts of an 64 bit integer.
    /// </summary>
    /// <param name="Lower">The lower part.</param>
    /// <param name="Upper">The upper part.</param>
    public readonly record struct IntegerParts(uint Lower, uint Upper)
    {
        /// <summary>
        /// Converts the given value into lower and upper parts.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        public IntegerParts(ulong value)
            : this((uint)(value & 0xffffffff), (uint)(value >> 32))
        { }

        /// <summary>
        /// Converts the parts into a single ulong value.
        /// </summary>
        /// <returns>The resolved ulong value.</returns>
        public ulong ToULong() => ((ulong)Upper << 32) | Lower;
    }

    /// <summary>
    /// Decomposes the given integer value into a lower and an upper part.
    /// </summary>
    /// <param name="value">The value to decompose.</param>
    /// <returns>The lower and upper part.</returns>
    public static IntegerParts Decompose(long value) => Decompose((ulong)value);

    /// <summary>
    /// Decomposes the given integer value into a lower and an upper part.
    /// </summary>
    /// <param name="value">The value to decompose.</param>
    /// <returns>The lower and upper part.</returns>
    public static IntegerParts Decompose(ulong value) => new(value);

    /// <summary>
    /// Composes an integer from the given lower and upper parts.
    /// </summary>
    /// <param name="parts">The lower and upper parts.</param>
    /// <returns>The composed integer.</returns>
    public static ulong ComposeULong(IntegerParts parts) => parts.ToULong();

    /// <summary>
    /// Composes an integer from the given lower and upper parts.
    /// </summary>
    /// <param name="parts">The lower and upper parts.</param>
    /// <returns>The composed integer.</returns>
    public static long ComposeLong(IntegerParts parts) => (long)ComposeULong(parts);
}
