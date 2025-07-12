// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BitOperations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.Util;
using System.Runtime.CompilerServices;

//
// Based on the references:
// - http://aggregate.org/MAGIC/#Trailing%20Zero%20Count
// - https://en.wikipedia.org/wiki/Hamming_weight.
// Previous implementation from XMath.BitOperations via ILGPU <= v1.5.x

namespace ILGPU;

partial class XMath
{
    #region PopCount

    /// <summary>
    /// Computes the number of one bits in the given 32-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of one bits.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(int value) => PopCount((uint)value);

    /// <summary>
    /// Computes the number of one bits in the given 32-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of one bits.</returns>
    /// <returns>The number of one bits.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(uint value)
    {
        value -= (value >> 1) & 0x55555555U;
        value = (value & 0x33333333U) + ((value >> 2) & 0x33333333U);
        value = (value + (value >> 4)) & 0x0F0F0F0FU;
        return (int)((value * 0x01010101U) >> 24);
    }

    /// <summary>
    /// Computes the number of one bits in the given 64-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of one bits.</returns>
    /// <returns>The number of one bits.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(long value) => PopCount((ulong)value);

    /// <summary>
    /// Computes the number of one bits in the given 64-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of one bits.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(ulong value)
    {
        value -= (value >> 1) & 0x5555555555555555UL;
        value = (value & 0x3333333333333333UL) +
            ((value >> 2) & 0x3333333333333333UL);
        value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        return (int)((value * 0x0101010101010101UL) >> 56);
    }

    #endregion

    #region LeadingZeroCount

    /// <summary>
    /// Returns the number of leading zeros in the given 32-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of leading zeros.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZeroCount(int value) =>
        LeadingZeroCount((uint)value);

    /// <summary>
    /// Returns the number of leading zeros in the given 32-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of leading zeros.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZeroCount(uint value)
    {
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return PopCount(~value);
    }

    /// <summary>
    /// Returns the number of leading zeros in the given 64-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of leading zeros.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZeroCount(long value) =>
        LeadingZeroCount((ulong)value);

    /// <summary>
    /// Returns the number of leading zeros in the given 64-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of leading zeros.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZeroCount(ulong value)
    {
        var parts = Decompose(value);
        return Utilities.Select(
            parts.Upper != 0,
            LeadingZeroCount(parts.Upper),
            32 + LeadingZeroCount(parts.Lower));
    }

    #endregion

    #region TrailingZeroCount

    /// <summary>
    /// Returns the number of trailing zeros in the given 32-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of trailing zeros.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(int value) =>
        PopCount((value & -value) - 1);

    /// <summary>
    /// Returns the number of trailing zeros in the given 32-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of trailing zeros.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(uint value) =>
        TrailingZeroCount((int)value);

    /// <summary>
    /// Returns the number of trailing zeros in the given 64-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of trailing zeros.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(long value) =>
        PopCount((value & -value) - 1L);

    /// <summary>
    /// Returns the number of trailing zeros in the given 64-bit integer value.
    /// </summary>
    /// <param name="value">The value to use.</param>
    /// <returns>The number of trailing zeros.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TrailingZeroCount(ulong value) =>
        TrailingZeroCount((long)value);

    #endregion
}
