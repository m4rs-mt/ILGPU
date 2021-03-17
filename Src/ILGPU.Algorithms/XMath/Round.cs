// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: Round.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Intrinsics;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded to even).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Round(double value) =>
            Round(value, 0, MidpointRounding.ToEven);

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded to even).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float value) =>
            Round(value, 0, MidpointRounding.ToEven);

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded to even).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="digits">
        /// The number of fractional digits in the return value.
        /// </param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Round(double value, int digits) =>
            Round(value, digits, MidpointRounding.ToEven);

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded to even).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="digits">
        /// The number of fractional digits in the return value.
        /// </param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float value, int digits) =>
            Round(value, digits, MidpointRounding.ToEven);

        /// <summary>
        /// Rounds the value to the nearest value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="mode">
        /// Specifiies how to round value if it is midway between two numbers.
        /// </param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Round(double value, MidpointRounding mode) =>
            Round(value, 0, mode);

        /// <summary>
        /// Rounds the value to the nearest value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="mode">
        /// Specifiies how to round value if it is midway between two numbers.
        /// </param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float value, MidpointRounding mode) =>
            Round(value, 0, mode);

        /// <summary>
        /// Rounds the value to the nearest value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="digits">
        /// The number of fractional digits in the return value.
        /// </param>
        /// <param name="mode">
        /// Specifiies how to round value if it is midway between two numbers.
        /// </param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Round(double value, int digits, MidpointRounding mode) =>
            RoundingModes.Round(value, digits, mode);

        /// <summary>
        /// Rounds the value to the nearest value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="digits">
        /// The number of fractional digits in the return value.
        /// </param>
        /// <param name="mode">
        /// Specifiies how to round value if it is midway between two numbers.
        /// </param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float value, int digits, MidpointRounding mode) =>
            RoundingModes.Round(value, digits, mode);

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded away from
        /// zero).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static double RoundAwayFromZero(double value) =>
            Math.Round(value, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded away from
        /// zero).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static float RoundAwayFromZero(float value) =>
#if !NETFRAMEWORK
            MathF.Round(value, MidpointRounding.AwayFromZero);
#else
            (float)Math.Round(value, MidpointRounding.AwayFromZero);
#endif

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded to even).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static double RoundToEven(double value) =>
            Math.Round(value, MidpointRounding.ToEven);

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded to even).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static float RoundToEven(float value) =>
#if !NETFRAMEWORK
            MathF.Round(value, MidpointRounding.ToEven);
#else
            (float)Math.Round(value, MidpointRounding.ToEven);
#endif

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
