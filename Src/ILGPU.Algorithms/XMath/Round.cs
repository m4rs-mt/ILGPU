// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: Round.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded away from zero).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double RoundAwayFromZero(double value) =>
            Utilities.Select(value < 0.0, Floor(value), Ceiling(value));

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded away from zero).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RoundAwayFromZero(float value) =>
            Utilities.Select(value < 0.0f, Floor(value), Ceiling(value));

        /// <summary>
        /// Truncates the given value.
        /// </summary>
        /// <param name="value">The value to truncate.</param>
        /// <returns>The truncated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Truncate(double value) =>
            Utilities.Select(value < 0.0, Ceiling(value), Floor(value));

        /// <summary>
        /// Truncates the given value.
        /// </summary>
        /// <param name="value">The value to truncate.</param>
        /// <returns>The truncated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Truncate(float value) =>
            Utilities.Select(value < 0.0f, Ceiling(value), Floor(value));
    }
}
