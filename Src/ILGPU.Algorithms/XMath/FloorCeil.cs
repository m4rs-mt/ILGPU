// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: FloorCeil.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Computes floor(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>floor(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Floor(double value) =>
            IntrinsicMath.CPUOnly.Floor(value);

        /// <summary>
        /// Computes floor(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>floor(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float value) =>
            IntrinsicMath.CPUOnly.Floor(value);

        /// <summary>
        /// Computes ceiling(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>ceiling(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Ceiling(double value) =>
            IntrinsicMath.CPUOnly.Ceiling(value);

        /// <summary>
        /// Computes ceiling(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>ceiling(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceiling(float value) =>
            IntrinsicMath.CPUOnly.Ceiling(value);
    }
}
