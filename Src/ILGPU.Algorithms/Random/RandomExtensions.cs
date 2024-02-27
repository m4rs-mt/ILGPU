// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: RandomExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// Represents useful helpers for random generators.
    /// </summary>
    public static partial class RandomExtensions
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
        internal static ulong SeparateUInt(uint nextUInt) =>
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
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
        /// <returns>A random int in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43Float8 Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            Mini43Float8 minValue,
            Mini43Float8 maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            float dist = (float)(maxValue - minValue);
            return (Mini43Float8) Math.Min(
                randomProvider.NextFloat() * dist + (float) minValue,
                (float) maxValue);
        }


        /// <summary>
        /// Generates a random int in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
        /// <returns>A random int in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini52Float8 Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            Mini52Float8 minValue,
            Mini52Float8 maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            float dist = (float)(maxValue - minValue);
            return (Mini52Float8) Math.Min(
                randomProvider.NextFloat() * dist + (float) minValue,
                (float) maxValue);
        }

        /// <summary>
        /// Generates a random int in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
        /// <returns>A random int in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            BFloat16 minValue,
            BFloat16 maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            float dist = (float)(maxValue - minValue);
            return (BFloat16) Math.Min(
                 randomProvider.NextFloat() * dist + (float) minValue,
                (float) maxValue);
        }

        /// <summary>
        /// Generates a random int in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
        /// <returns>A random int in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            Half minValue,
            Half maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            float dist = (float)(maxValue - minValue);
            return (Half) Math.Min(
                randomProvider.NextFloat() * dist +(float) minValue,
                (float) maxValue);
        }

        /// <summary>
        /// Generates a random int in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
        /// <returns>A random int in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            float minValue,
            float maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            float dist = maxValue - minValue;
            return Math.Min(
                randomProvider.NextFloat() * dist + minValue,
                maxValue);
        }

        /// <summary>
        /// Generates a random int in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
        /// <returns>A random int in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            double minValue,
            double maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            double dist = maxValue - minValue;
            return Math.Min(
                randomProvider.NextDouble() * dist + minValue,
                maxValue);
        }

        /// <summary>
        /// Generates a random int in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
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
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
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
        /// Generates a random long in [minValue..maxValue).
        /// </summary>
        /// <param name="randomProvider">The random provider.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum values (exclusive).</param>
        /// <returns>A random long in [minValue..maxValue).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Next<TRandomProvider>(
            ref TRandomProvider randomProvider,
            ulong minValue,
            ulong maxValue)
            where TRandomProvider : struct, IRandomProvider
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            ulong dist = maxValue - minValue;

            // Check whether the bit range matches in theory
            ulong intermediate = dist > 1UL << 23
                ? (ulong)(randomProvider.NextFloat() * dist)
                : (ulong)(randomProvider.NextDouble() * dist);
            return Math.Min(intermediate + minValue, maxValue - 1UL);
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Generates a new random vector containing provided RNG-based values.
        /// </summary>
        /// <typeparam name="T">The vector element type.</typeparam>
        /// <typeparam name="TRandomProvider">The RNG provider type.</typeparam>
        /// <typeparam name="TRange">The generic RNG value range.</typeparam>
        /// <param name="randomProvider">The random provider instance to use.</param>
        /// <param name="range">The generic range instance to use.</param>
        /// <returns>The created random vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> NextVector<T, TRandomProvider, TRange>(
            ref TRandomProvider randomProvider,
            TRange range)
            where T : unmanaged
            where TRandomProvider : struct, IRandomProvider
            where TRange : struct, IRandomRange<T>
        {
            int vectorLength = Vector<T>.Count;
            int length = Interop.SizeOf<T>() * vectorLength;

            // Allocate temporary buffers
            var source = stackalloc byte[length + vectorLength];
            var span = new Span<T>(
                (void*)Interop.Align((long)source, length, vectorLength),
                vectorLength);

            // Generated random numbers
            for (int i = 0; i < vectorLength; ++i)
                span[i] = range.Next(ref randomProvider);

            // Load aligned vector
            return span.LoadAlignedVectorUnsafe();
        }

        /// <summary>
        /// Generates a new random vector containing provided RNG-based values.
        /// </summary>
        /// <typeparam name="T">The vector element type.</typeparam>
        /// <typeparam name="TRangeProvider">The RNG range provider.</typeparam>
        /// <param name="rangeProvider">The range provider instance to use.</param>
        /// <returns>The created random vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> NextVector<T, TRangeProvider>(
            ref TRangeProvider rangeProvider)
            where T : unmanaged
            where TRangeProvider : struct, IRandomRangeProvider<TRangeProvider, T>
        {
            int vectorLength = Vector<T>.Count;
            int length = Interop.SizeOf<T>() * vectorLength;

            // Allocate temporary buffers
            var source = stackalloc byte[length + vectorLength];
            var span = new Span<T>(
                (void*)Interop.Align((long)source, length, vectorLength),
                vectorLength);

            // Generated random numbers
            for (int i = 0; i < vectorLength; ++i)
                span[i] = rangeProvider.Next();

            // Load aligned vector
            return span.LoadAlignedVectorUnsafe();
        }
#endif

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

        /// <summary>
        /// Initialization kernel used to initialize the actual RNG values on the device.
        /// </summary>
        internal static void InitializeRNGKernel<TRandomProvider>(
            Index1D index,
            ArrayView<XorShift128Plus> sourceProviders,
            ArrayView<TRandomProvider> randomProvider)
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
        {
            var provider = sourceProviders[index];
            for (LongIndex1D i = index;
                i < randomProvider.Length;
                i += GridExtensions.GridStrideLoopStride)
            {
                randomProvider[i] = default(TRandomProvider).CreateProvider(ref provider);
            }

            // Update provider state for future iterations
            sourceProviders[index] = provider;
        }

        /// <summary>
        /// Initializes a given view with random values.
        /// </summary>
        /// <typeparam name="TRandomProvider">
        /// The RNG provider type to use on the device.
        /// </typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="rngView">The view to fill.</param>
        /// <param name="random">The source RNG provider.</param>
        /// <param name="numInitializers">The number of CPU initializers to use.</param>
        public static void InitRNGView<TRandomProvider>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<TRandomProvider> rngView,
            System.Random random,
            int numInitializers = ushort.MaxValue)
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
        {
            // Initialize a single provider per warp
            numInitializers = (int)Math.Min(XMath.Clamp(
                    numInitializers,
                    accelerator.MaxNumThreadsPerGroup,
                    rngView.Length),
                ushort.MaxValue);
            var providers = new XorShift128Plus[numInitializers];
            for (int i = 0; i < numInitializers; ++i)
                providers[i] = default(XorShift128Plus).CreateProvider(random);

            // Initialize all providers in our buffer
            using var tempProviderBuffer = accelerator.Allocate1D(providers);
            accelerator.LaunchAutoGrouped(
                InitializeRNGKernel,
                stream,
                new Index1D(providers.Length),
                tempProviderBuffer.View.AsContiguous(),
                rngView);
        }
    }
}
