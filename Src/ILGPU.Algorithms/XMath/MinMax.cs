// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: MinMax.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    partial class XMath
    {
        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Min(double first, double second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Min(float first, float second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Min(sbyte first, sbyte second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Min(short first, short second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Min(int first, int second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Min(long first, long second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Min(byte first, byte second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Min(ushort first, ushort second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Min(uint first, uint second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Min(ulong first, ulong second) =>
            IntrinsicMath.Min(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Max(double first, double second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Max(float first, float second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Max(sbyte first, sbyte second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Max(short first, short second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Max(int first, int second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Max(long first, long second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Max(byte first, byte second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Max(ushort first, ushort second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Max(uint first, uint second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Max(ulong first, ulong second) =>
            IntrinsicMath.Max(first, second);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double value, double min, double max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Clamp(sbyte value, sbyte min, sbyte max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Clamp(short value, short min, short max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int value, int min, int max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Clamp(long value, long min, long max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Clamp(byte value, byte min, byte max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Clamp(ushort value, ushort min, ushort max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Clamp(uint value, uint min, uint max) =>
            IntrinsicMath.Clamp(value, min, max);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Clamp(ulong value, ulong min, ulong max) =>
            IntrinsicMath.Clamp(value, min, max);
    }
}
