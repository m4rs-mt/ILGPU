// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericWarp.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.RadixSort;
using ILGPU.ScanReduce;
using ILGPU.Util;
using System.Runtime.CompilerServices;

namespace ILGPUC.Intrinsic;

/// <summary>
/// Contains default high-level implementations for backends supporting warp features.
/// </summary>
static class GenericWarp
{
    #region Reduce

    /// <summary>
    /// Performs a warp-wide reduction.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>The first lane (lane id = 0) will return reduced result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Reduce<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T>
    {
        for (int laneOffset = Warp.Dimension / 2; laneOffset > 0; laneOffset >>= 1)
        {
            var shuffled = Warp.ShuffleDown(value, laneOffset);
            value = TReduction.Apply(value, shuffled);
        }
        return value;
    }

    /// <summary>
    /// Performs a warp-wide reduction.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>All lanes will return the reduced result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AllReduce<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T>
    {
        for (int laneMask = Warp.Dimension / 2; laneMask > 0; laneMask >>= 1)
        {
            var shuffled = Warp.ShuffleXor(value, laneMask);
            value = TReduction.Apply(value, shuffled);
        }
        return value;
    }

    #endregion

    #region Scan

    /// <summary>
    /// Performs a warp-wide exclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ExclusiveScan<T, TScan>(T value)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T>
    {
        var inclusive = InclusiveScan<T, TScan>(value);

        var exclusive = Warp.ShuffleUp(inclusive, 1);
        return Warp.IsFirstLane ? TScan.Identity : exclusive;
    }

    /// <summary>
    /// Performs a warp-wide inclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T InclusiveScan<T, TScan>(T value)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T>
    {
        var laneIdx = Warp.LaneIndex;
        for (int delta = 1; delta < Warp.Dimension; delta <<= 1)
        {
            var otherValue = Warp.ShuffleUp(value, delta);
            if (laneIdx >= delta)
                value = TScan.Apply(value, otherValue);
        }
        return value;
    }

    #endregion

    #region Sort

    /// <summary>
    /// Performs a warp-wide radix sort operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The radix sort operation type.
    /// </typeparam>
    /// <param name="value">The original value in the current lane.</param>
    /// <returns>The sorted value for the current lane.</returns>
    public static T RadixSort<T, TRadixSortOperation>(T value)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T>
    {
        for (int bitIdx = 0; bitIdx < TRadixSortOperation.NumBits; ++bitIdx)
        {
            var key = TRadixSortOperation.ExtractRadixBits(value, bitIdx, 1);
            var key0 = key == 0 ? 1 : 0;
            var key1 = 1 - key0;

            for (int offset = 1; offset < Warp.Dimension - 1; offset <<= 1)
            {
                var partialKey0 = Warp.ShuffleUp(key0, offset);
                var partialKey1 = Warp.ShuffleUp(key1, offset);
                key0 += Utilities.Select(Warp.LaneIndex >= offset, partialKey0, 0);
                key1 += Utilities.Select(Warp.LaneIndex >= offset, partialKey1, 0);
            }
            key1 += Warp.Shuffle(key0, Warp.Dimension - 1);

            var target = key == 0 ? key0 - 1 : key1 - 1;
            T newElement = TRadixSortOperation.DefaultValue;
            for (int k = 0; k < Warp.Dimension; k++)
            {
                var targetLane = Warp.Shuffle(target, k);
                var retrievedElement = Warp.Shuffle(value, k);
                if (targetLane == Warp.LaneIndex)
                    newElement = retrievedElement;
            }

            value = newElement;
        }
        return value;
    }

    #endregion
}
