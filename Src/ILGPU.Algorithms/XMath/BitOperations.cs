// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: BitOperations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        #region PopCount

        /// <summary>
        /// Computes the number of one bits in the given 32-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of one bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(int value) => IntrinsicMath.PopCount(value);

        /// <summary>
        /// Computes the number of one bits in the given 32-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of one bits.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(uint value) => IntrinsicMath.PopCount(value);

        /// <summary>
        /// Computes the number of one bits in the given 64-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of one bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(long value) => IntrinsicMath.PopCount(value);

        /// <summary>
        /// Computes the number of one bits in the given 64-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of one bits.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(ulong value) =>
            IntrinsicMath.PopCount(value);

        #endregion

        #region LeadingZeroCount

        /// <summary>
        /// Returns the number of leading zeros in the given 32-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of leading zeros.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(int value) =>
            IntrinsicMath.LeadingZeroCount(value);

        /// <summary>
        /// Returns the number of leading zeros in the given 32-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of leading zeros.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(uint value) =>
            IntrinsicMath.LeadingZeroCount(value);

        /// <summary>
        /// Returns the number of leading zeros in the given 64-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of leading zeros.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(long value) =>
            IntrinsicMath.LeadingZeroCount(value);

        /// <summary>
        /// Returns the number of leading zeros in the given 64-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of leading zeros.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(ulong value) =>
            IntrinsicMath.LeadingZeroCount(value);

        #endregion

        #region TrailingZeroCount

        /// <summary>
        /// Returns the number of trailing zeros in the given 32-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of trailing zeros.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(int value) =>
            IntrinsicMath.TrailingZeroCount(value);

        /// <summary>
        /// Returns the number of trailing zeros in the given 32-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of trailing zeros.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(uint value) =>
            IntrinsicMath.TrailingZeroCount(value);

        /// <summary>
        /// Returns the number of trailing zeros in the given 64-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of trailing zeros.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(long value) =>
            IntrinsicMath.TrailingZeroCount(value);

        /// <summary>
        /// Returns the number of trailing zeros in the given 64-bit integer value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <returns>The number of trailing zeros.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(ulong value) =>
            IntrinsicMath.TrailingZeroCount(value);

        #endregion
    }
}
