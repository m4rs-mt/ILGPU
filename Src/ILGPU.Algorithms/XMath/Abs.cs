// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Abs.cs
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
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Abs(double value) =>
            IntrinsicMath.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Abs(float value) =>
            IntrinsicMath.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half Abs(Half value) =>
            IntrinsicMath.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Abs(sbyte value) =>
            IntrinsicMath.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Abs(short value) =>
            IntrinsicMath.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Abs(int value) =>
            IntrinsicMath.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Abs(long value) =>
            IntrinsicMath.Abs(value);
    }
}
