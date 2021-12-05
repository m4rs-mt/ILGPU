// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NaN.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Returns true iff the given value is NaN.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is NaN.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaN(double value) =>
            IntrinsicMath.CPUOnly.IsNaN(value);

        /// <summary>
        /// Returns true iff the given value is NaN.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is NaN.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaN(float value) =>
            IntrinsicMath.CPUOnly.IsNaN(value);

        /// <summary>
        /// Returns true iff the given value is infinity.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInfinity(double value) =>
            IntrinsicMath.CPUOnly.IsInfinity(value);

        /// <summary>
        /// Returns true iff the given value is infinity.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInfinity(float value) =>
            IntrinsicMath.CPUOnly.IsInfinity(value);

        /// <summary>
        /// Returns true iff the given value is finite.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is finite.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double value) =>
            IntrinsicMath.CPUOnly.IsFinite(value);

        /// <summary>
        /// Returns true iff the given value is finite.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is finite.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float value) =>
            IntrinsicMath.CPUOnly.IsFinite(value);
    }
}
