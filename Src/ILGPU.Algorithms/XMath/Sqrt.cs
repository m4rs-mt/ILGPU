// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: Sqrt.cs
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
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>sqrt(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sqrt(double value) =>
            IntrinsicMath.CPUOnly.Sqrt(value);

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>sqrt(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sqrt(float value) =>
            IntrinsicMath.CPUOnly.Sqrt(value);

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1/sqrt(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Rsqrt(double value) =>
            IntrinsicMath.CPUOnly.Rsqrt(value);

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1/sqrt(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Rsqrt(float value) =>
            IntrinsicMath.CPUOnly.Rsqrt(value);
    }
}
