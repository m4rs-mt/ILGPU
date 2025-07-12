// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: RandomExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU.Vectors;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU.Random;

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

    /// <summary>
    /// A random number provider for generic types.
    /// </summary>
    /// <typeparam name="T">The random type to be generated.</typeparam>
    /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
    /// <param name="randomProvider">The random provider to use.</param>
    /// <returns>The generated random value.</returns>
    public delegate T RandomRangeProvider<T, TRandomProvider>(
        ref TRandomProvider randomProvider)
        where T : struct
        where TRandomProvider : struct, IRandomProvider;

    /// <summary>
    /// Generates a new random vector containing provided RNG-based values.
    /// </summary>
    /// <typeparam name="T">The vector element type.</typeparam>
    /// <typeparam name="TRandomProvider">The RNG provider type.</typeparam>
    /// <param name="randomProvider">The random provider instance to use.</param>
    /// <param name="rangeProvider">The random range provider.</param>
    /// <returns>The created random vector.</returns>
    public static unsafe Vector<T> NextVector<T, TRandomProvider>(
        ref TRandomProvider randomProvider,
        RandomRangeProvider<T, TRandomProvider> rangeProvider)
        where T : unmanaged
        where TRandomProvider : struct, IRandomProvider
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
            span[i] = rangeProvider(ref randomProvider);

        // Load aligned vector
        return span.LoadAlignedVectorUnsafe();
    }

    /// <summary>
    /// Fills the given array view with uniformly sampled random values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">The RNG provider type.</typeparam>
    /// <param name="arrayView">The array view to fill.</param>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="provider">The random range sampler to use.</param>
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public static void FillUniform<T, TRandomProvider>(
        this AcceleratorStream stream,
        ArrayView<T> arrayView,
        RandomRangeProvider<T, TRandomProvider> provider)
        where T : unmanaged
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
        stream.Launch(arrayView.Extent, index =>
        {
            var warpProvider = Warp.GetRandom<TRandomProvider>();
            arrayView[index] = provider(ref warpProvider.RandomProvider);
            warpProvider.Dispose();
        });
}
