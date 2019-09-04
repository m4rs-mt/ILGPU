// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: Trig.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Computes sin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sin(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sin(double value) =>
            IntrinsicMath.CPUOnly.Sin(value);

        /// <summary>
        /// Computes sin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sin(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float value) =>
            IntrinsicMath.CPUOnly.Sin(value);

        /// <summary>
        /// Computes sinh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sinh(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sinh(double value) =>
            IntrinsicMath.CPUOnly.Sinh(value);

        /// <summary>
        /// Computes asin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>asin(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Asin(double value) =>
            IntrinsicMath.CPUOnly.Asin(value);

        /// <summary>
        /// Computes asin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>asin(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Asin(float value) =>
            IntrinsicMath.CPUOnly.Asin(value);

        /// <summary>
        /// Computes sinh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sinh(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sinh(float value) =>
            IntrinsicMath.CPUOnly.Sinh(value);

        /// <summary>
        /// Computes cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cos(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cos(double value) =>
            IntrinsicMath.CPUOnly.Cos(value);

        /// <summary>
        /// Computes cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cos(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float value) =>
            IntrinsicMath.CPUOnly.Cos(value);

        /// <summary>
        /// Computes cosh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cosh(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cosh(double value) =>
            IntrinsicMath.CPUOnly.Cosh(value);

        /// <summary>
        /// Computes cosh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cosh(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cosh(float value) =>
            IntrinsicMath.CPUOnly.Cosh(value);

        /// <summary>
        /// Computes acos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>acos(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Acos(double value) =>
            IntrinsicMath.CPUOnly.Acos(value);

        /// <summary>
        /// Computes acos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>acos(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Acos(float value) =>
            IntrinsicMath.CPUOnly.Acos(value);

        /// <summary>
        /// Computes tan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tan(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Tan(double value) =>
            IntrinsicMath.CPUOnly.Tan(value);

        /// <summary>
        /// Computes tan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tan(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tan(float value) =>
            IntrinsicMath.CPUOnly.Tan(value);

        /// <summary>
        /// Computes tanh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tanh(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Tanh(double value) =>
            IntrinsicMath.CPUOnly.Tanh(value);

        /// <summary>
        /// Computes tanh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tanh(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tanh(float value) =>
            IntrinsicMath.CPUOnly.Tanh(value);

        /// <summary>
        /// Computes atan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>atan(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Atan(double value) =>
            IntrinsicMath.CPUOnly.Atan(value);

        /// <summary>
        /// Computes atan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>atan(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan(float value) =>
            IntrinsicMath.CPUOnly.Atan(value);

        /// <summary>
        /// Computes atan2(y, x).
        /// </summary>
        /// <param name="y">The y value in radians.</param>
        /// <param name="x">The x value in radians.</param>
        /// <returns>atan2(y, x).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Atan2(double y, double x) =>
            IntrinsicMath.CPUOnly.Atan2(y, x);

        /// <summary>
        /// Computes atan2(y, x).
        /// </summary>
        /// <param name="y">The y value in radians.</param>
        /// <param name="x">The x value in radians.</param>
        /// <returns>atan2(y, x).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan2(float y, float x) =>
            IntrinsicMath.CPUOnly.Atan2(y, x);

        /// <summary>
        /// Computes sin(value) and cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <param name="sin">The result of sin(value).</param>
        /// <param name="cos">The result of cos(value).</param>
        /// <returns>tanh(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SinCos(double value, out double sin, out double cos)
        {
            sin = Sin(value);
            cos = Cos(value);
        }

        /// <summary>
        /// Computes sin(value) and cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <param name="sin">The result of sin(value).</param>
        /// <param name="cos">The result of cos(value).</param>
        /// <returns>tanh(value).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SinCos(float value, out float sin, out float cos)
        {
            sin = Sin(value);
            cos = Cos(value);
        }
    }
}
