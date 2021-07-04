// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: Pow.cs
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
        /// Computes basis^exp.
        /// </summary>
        /// <param name="base">The basis.</param>
        /// <param name="exp">The exponent.</param>
        /// <returns>pow(basis, exp).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Pow(double @base, double exp) =>
            IntrinsicMath.CPUOnly.Pow(@base, exp);

        /// <summary>
        /// Computes basis^exp.
        /// </summary>
        /// <param name="base">The basis.</param>
        /// <param name="exp">The exponent.</param>
        /// <returns>pow(basis, exp).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow(float @base, float exp) =>
            IntrinsicMath.CPUOnly.Pow(@base, exp);

        /// <summary>
        /// Computes exp(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>exp(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exp(double value) =>
            IntrinsicMath.CPUOnly.Exp(value);

        /// <summary>
        /// Computes exp(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>exp(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float value) =>
            IntrinsicMath.CPUOnly.Exp(value);

        /// <summary>
        /// Computes 2^value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>2^value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Exp2(double value) =>
            IntrinsicMath.CPUOnly.Exp2(value);

        /// <summary>
        /// Computes 2^value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>2^value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp2(float value) =>
            IntrinsicMath.CPUOnly.Exp2(value);
    }
}
