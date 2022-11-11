// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: RandomExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// Represents useful helpers for random generators.
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>
        /// 1.0 / int.MaxValue
        /// </summary>
        internal const double InverseIntDoubleRange = 1.0 / int.MaxValue;
        /// <summary>
        /// 1.0 / int.MaxValue
        /// </summary>
        internal const float InverseIntFloatRange = (float)InverseIntDoubleRange;

        /// <summary>
        /// 1.0 / long.MaxValue
        /// </summary>
        internal const double InverseLongDoubleRange = 1.0 / long.MaxValue;

        /// <summary>
        /// 1.0 / long.MaxValue
        /// </summary>
        internal const float InverseLongFloatRange = (float)InverseLongDoubleRange;

        /// <summary>
        /// Merges the given unsigned long into an unsigned integer.
        /// </summary>
        internal static uint MergeULong(ulong nextULong) =>
            (uint)((nextULong >> 32) ^ nextULong);

        /// <summary>
        /// Separates the given unsigned int into an unsigned long.
        /// </summary>
        internal static ulong SeperateUInt(uint nextUInt) =>
            ((ulong)nextUInt << 32) | nextUInt;

        /// <summary>
        /// Converts the given unsigned int into a positive signed integer.
        /// </summary>
        internal static int ToInt(uint nextUInt) => (int)(nextUInt & 0x7FFFFFFFU);

        /// <summary>
        /// Converts the given unsigned long into a positive signed long.
        /// </summary>
        internal static long ToLong(ulong nextULong) =>
            (long)(nextULong & 0x7FFFFFFFFFFFFFFFUL);

        /// <summary>
        /// Shifts an RNG state to be used within a warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ShiftState(uint state, int laneShift)
        {
            Trace.Assert(laneShift >= 0 & laneShift < 128, "Invalid shift amount");

            uint shiftVal = (uint)laneShift;
            return (state + (shiftVal << 7) + (shiftVal >> 3)) | shiftVal;
        }

        /// <summary>
        /// Shifts an RNG state to be used within a warp.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ShiftState(ulong state, int laneShift)
        {
            Trace.Assert(laneShift >= 0 & laneShift < 128, "Invalid shift amount");

            ulong shiftVal = (ulong)laneShift;
            return (state + (shiftVal << 7) + (shiftVal >> 3)) | shiftVal;
        }

        /// <summary>
        /// Generates a random int in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive)</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <returns>A random int in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            int minValue,
            int maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            int dist = maxValue - minValue;
            return Math.Min(
                (int)(randomProvider.NextFloat() * dist) + minValue,
                maxValue - 1);
        }

        /// <summary>
        /// Generates a random long in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive)</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <returns>A random long in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            long minValue,
            long maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            long dist = maxValue - minValue;

            // Check whether the bit range matches in theory
            long intermediate = Math.Abs(dist) > 1L << 23
                ? (long)(randomProvider.NextFloat() * dist)
                : (long)(randomProvider.NextDouble() * dist);
            return Math.Min(intermediate + minValue, maxValue - 1);
        }

        /// <summary>
        /// Constructs an RNG using the given provider instance.
        /// </summary>
        /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="random">The parent RNG provider.</param>
        public static RNG<TRandomProvider> CreateRNG<TRandomProvider>(
            this Accelerator accelerator,
            System.Random random)
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
            RNG.Create<TRandomProvider>(accelerator, random);

        /// <summary>
        /// Constructs an RNG using the given provider instance.
        /// </summary>
        /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="random">The parent RNG provider.</param>
        /// <param name="maxNumParallelWarps">
        /// The maximum number of parallel warps.
        /// </param>
        public static RNG<TRandomProvider> CreateRNG<TRandomProvider>(
            this Accelerator accelerator,
            System.Random random,
            int maxNumParallelWarps)
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
            RNG.Create<TRandomProvider>(accelerator, random, maxNumParallelWarps);
    }
}
