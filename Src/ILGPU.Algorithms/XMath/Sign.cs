// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Sign.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Computes the sign of the provided value.
        /// Sign will return 0 for NaN, Infinity or 0 values.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>-1 for negative value, 1 for positive values, and 0 for
        /// 0, NaN or Infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(double value) =>
            Utilities.Select(value < 0.0, -1, Utilities.Select(value > 0.0, 1, 0));

        /// <summary>
        /// Computes the sign of the provided value.
        /// Sign will return 0 for NaN, Infinity or 0 values.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>-1 for negative value, 1 for positive values, and 0 for
        /// 0, NaN or Infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(float value) =>
            Utilities.Select(value < 0.0f, -1, Utilities.Select(value > 0.0f, 1, 0));

        /// <summary>
        /// Computes the sign of the provided value.
        /// Sign will return 0 for NaN, Infinity or 0 values.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>-1 for negative value, 1 for positive values, and 0 for
        /// 0, NaN or Infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(Half value) =>
            Utilities.Select(
        value < Half.Zero,
        -1,
        Utilities.Select(value > Half.Zero, 1, 0));

    }
}
