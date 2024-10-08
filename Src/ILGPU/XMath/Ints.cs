// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Ints.cs
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
            IntrinsicMath.DivRoundDown(numerator, denominator);

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// up (away from zero).
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns
        /// >The numerator divided by the denominator rounded up (away from zero).
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivRoundUp(int numerator, int denominator) =>
            IntrinsicMath.DivRoundUp(numerator, denominator);


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
            IntrinsicMath.DivRoundDown(numerator, denominator);


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
            IntrinsicMath.DivRoundUp(numerator, denominator);

        /// <summary>
        /// Decomposes the given integer value into a lower and an upper part.
        /// </summary>
        /// <param name="value">The value to decompose.</param>
        /// <returns>The lower and upper part.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntrinsicMath.IntegerParts Decompose(long value) =>
            IntrinsicMath.Decompose(value);

        /// <summary>
        /// Decomposes the given integer value into a lower and an upper part.
        /// </summary>
        /// <param name="value">The value to decompose.</param>
        /// <returns>The lower and upper part.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntrinsicMath.IntegerParts Decompose(ulong value) =>
            IntrinsicMath.Decompose(value);

        /// <summary>
        /// Composes an integer from the given lower and upper parts.
        /// </summary>
        /// <param name="parts">The lower and upper parts.</param>
        /// <returns>The composed integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComposeULong(IntrinsicMath.IntegerParts parts) =>
            parts.ToULong();

        /// <summary>
        /// Composes an integer from the given lower and upper parts.
        /// </summary>
        /// <param name="parts">The lower and upper parts.</param>
        /// <returns>The composed integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ComposeLong(IntrinsicMath.IntegerParts parts) =>
            (long)parts.ToULong();
    }
}
