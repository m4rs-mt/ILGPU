// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Utilities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Util
{
    /// <summary>
    /// General utility methods.
    /// </summary>
    public static class Utilities
    {
        /// <summary>
        /// Swaps the given values.
        /// </summary>
        /// <typeparam name="T">The type of the values.</typeparam>
        /// <param name="first">The first value to swap with the second one.</param>
        /// <param name="second">The second value to swap with the first one.</param>
        public static void Swap<T>(ref T first, ref T second) =>
            (first, second) = (second, first);

        /// <summary>
        /// Swaps the given values if swap is true.
        /// </summary>
        /// <typeparam name="T">The type of the values.</typeparam>
        /// <param name="performSwap">True, if the values should be swapped.</param>
        /// <param name="first">The first value to swap with the second one.</param>
        /// <param name="second">The second value to swap with the first one.</param>
        /// <returns>True, if the values were swapped.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Swap<T>(bool performSwap, ref T first, ref T second)
        {
            if (!performSwap)
                return false;
            Swap(ref first, ref second);
            return true;
        }

        /// <summary>
        /// Selects between the two given values.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="takeFirst">
        /// True, if the
        /// <paramref name="first"/> value should be taken.</param>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The selected value.</returns>
        /// <remarks>
        /// Note that this function will be mapped to the ILGPU IR.
        /// </remarks>
        [UtilityIntrinsic(UtilityIntrinsicKind.Select)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Select<T>(bool takeFirst, T first, T second) =>
            takeFirst ? first : second;

        /// <summary>
        /// Returns true if the given integer is a power of two.
        /// </summary>
        /// <param name="value">The integer value.</param>
        /// <returns>True, if the given integer is a power of two.</returns>
        public static bool IsPowerOf2(long value) =>
            value != long.MinValue && IsPowerOf2((ulong)IntrinsicMath.Abs(value));

        /// <summary>
        /// Returns true if the given integer is a power of two.
        /// </summary>
        /// <param name="value">The integer value.</param>
        /// <returns>True, if the given integer is a power of two.</returns>
        [CLSCompliant(false)]
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
    }
}
