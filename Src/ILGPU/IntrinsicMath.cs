﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IntrinsicMath.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using System;

namespace ILGPU
{
    /// <summary>
    /// Represents basic intrinsic math helpers for general
    /// math operations that are supported on the CPU and the GPU.
    /// </summary>
    /// <remarks>
    /// For more advanced math functions refer to the algorithms library.
    /// </remarks>
    public static partial class IntrinsicMath
    {
        #region Abs

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static double Abs(double value) =>
            Math.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static float Abs(float value) =>
            Math.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static Half Abs(Half value) =>
            Half.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [CLSCompliant(false)]
        public static sbyte Abs(sbyte value) =>
            (sbyte)Abs((int)value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        public static short Abs(short value) =>
            (short)Abs((int)value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static int Abs(int value) =>
            Math.Abs(value);

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static long Abs(long value) =>
            Math.Abs(value);

        #endregion

        #region Min/Max

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Min)]
        public static double Min(double first, double second) =>
            Math.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Min)]
        public static float Min(float first, float second) =>
            Math.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        public static sbyte Min(sbyte first, sbyte second) =>
            (sbyte)Min((int)first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        public static short Min(short first, short second) =>
            (short)Min((int)first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Min)]
        public static int Min(int first, int second) =>
            Math.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Min)]
        public static long Min(long first, long second) =>
            Math.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        public static byte Min(byte first, byte second) =>
            (byte)Min((uint)first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        public static ushort Min(ushort first, ushort second) =>
            (ushort)Min((uint)first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        [MathIntrinsic(MathIntrinsicKind.Min, ArithmeticFlags.Unsigned)]
        public static uint Min(uint first, uint second) =>
            Math.Min(first, second);

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        [MathIntrinsic(MathIntrinsicKind.Min, ArithmeticFlags.Unsigned)]
        public static ulong Min(ulong first, ulong second) =>
            Math.Min(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Max)]
        public static double Max(double first, double second) =>
            Math.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Max)]
        public static float Max(float first, float second) =>
            Math.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        public static sbyte Max(sbyte first, sbyte second) =>
            (sbyte)Max((int)first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        public static short Max(short first, short second) =>
            (short)Max((int)first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Max)]
        public static int Max(int first, int second) =>
            Math.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Max)]
        public static long Max(long first, long second) =>
            Math.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        public static byte Max(byte first, byte second) =>
            (byte)Max((uint)first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        public static ushort Max(ushort first, ushort second) =>
            (ushort)Max((uint)first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        [MathIntrinsic(MathIntrinsicKind.Max, ArithmeticFlags.Unsigned)]
        public static uint Max(uint first, uint second) =>
            Math.Max(first, second);

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        [MathIntrinsic(MathIntrinsicKind.Max, ArithmeticFlags.Unsigned)]
        public static ulong Max(ulong first, ulong second) =>
            Math.Max(first, second);

        #endregion

        #region Clamp

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static double Clamp(double value, double min, double max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static float Clamp(float value, float min, float max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        public static sbyte Clamp(sbyte value, sbyte min, sbyte max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static short Clamp(short value, short min, short max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static int Clamp(int value, int min, int max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static long Clamp(long value, long min, long max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static byte Clamp(byte value, byte min, byte max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        public static ushort Clamp(ushort value, ushort min, ushort max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        public static uint Clamp(uint value, uint min, uint max) =>
            Max(Min(value, max), min);

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        public static ulong Clamp(ulong value, ulong min, ulong max) =>
            Max(Min(value, max), min);

        #endregion

        #region Int Divisions

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// down to zero.
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The numerator divided by the denominator rounded to zero.</returns>
        public static int DivRoundDown(int numerator, int denominator) =>
            numerator / denominator;

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// up (away from zero).
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>
        /// The numerator divided by the denominator rounded up (away from zero).
        /// </returns>
        public static int DivRoundUp(int numerator, int denominator) =>
            (numerator + denominator - 1) / denominator;

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// down to zero.
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The numerator divided by the denominator rounded to zero.</returns>
        public static long DivRoundDown(long numerator, long denominator) =>
            numerator / denominator;

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// up (away from zero).
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>
        /// The numerator divided by the denominator rounded up (away from zero).
        /// </returns>
        public static long DivRoundUp(long numerator, long denominator) =>
            (numerator + denominator - 1L) / denominator;

        #endregion

        #region Compose & Decompose

        /// <summary>
        /// Represents integer parts of an 64 bit integer.
        /// </summary>
        [CLSCompliant(false)]
        public struct IntegerParts
        {
            /// <summary>
            /// Converts the given value into lower and upper parts.
            /// </summary>
            /// <param name="value">The value to convert.</param>
            public IntegerParts(ulong value)
                : this((uint)(value & 0xffffffff), (uint)(value >> 32))
            { }

            /// <summary>
            /// Stores the given lower and upper parts.
            /// </summary>
            /// <param name="lower">The lower part.</param>
            /// <param name="upper">The upper part.</param>
            public IntegerParts(uint lower, uint upper)
            {
                Lower = lower;
                Upper = upper;
            }

            /// <summary>
            /// The lower 32 bits.
            /// </summary>
            public uint Lower { get; set; }

            /// <summary>
            /// The upper 32 bits.
            /// </summary>
            public uint Upper { get; set; }

            /// <summary>
            /// Converts the parts into a single ulong value.
            /// </summary>
            /// <returns>The resolved ulong value.</returns>
            public ulong ToULong() => ((ulong)Upper << 32) | Lower;
        }

        /// <summary>
        /// Decomposes the given integer value into a lower and an upper part.
        /// </summary>
        /// <param name="value">The value to decompose.</param>
        /// <returns>The lower and upper part.</returns>
        [CLSCompliant(false)]
        public static IntegerParts Decompose(long value) => Decompose((ulong)value);

        /// <summary>
        /// Decomposes the given integer value into a lower and an upper part.
        /// </summary>
        /// <param name="value">The value to decompose.</param>
        /// <returns>The lower and upper part.</returns>
        [CLSCompliant(false)]
        public static IntegerParts Decompose(ulong value) =>
            new IntegerParts(value);

        /// <summary>
        /// Composes an integer from the given lower and upper parts.
        /// </summary>
        /// <param name="parts">The lower and upper parts.</param>
        /// <returns>The composed integer.</returns>
        [CLSCompliant(false)]
        public static ulong ComposeULong(IntegerParts parts) => parts.ToULong();

        /// <summary>
        /// Composes an integer from the given lower and upper parts.
        /// </summary>
        /// <param name="parts">The lower and upper parts.</param>
        /// <returns>The composed integer.</returns>
        [CLSCompliant(false)]
        public static long ComposeLong(IntegerParts parts) => (long)ComposeULong(parts);

        #endregion
    }
}
