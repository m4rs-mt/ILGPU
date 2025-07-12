// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericGroup.cs
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
/// Contains default high-level implementations for backends supporting group and warp
/// features to implement group-level intrinsics.
/// </summary>
static class GenericGroup
{
    #region Broadcast

    /// <summary>
    /// Specializes 32-bit broadcast functionality.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Broadcast32(uint value, int threadIndex)
    {
        ref var sharedMemory = ref Group.GetSharedMemory<uint>();
        if (Group.Index == threadIndex)
            sharedMemory = value;
        Group.Barrier();

        return sharedMemory;
    }

    /// <summary>
    /// Specializes 64-bit broadcast functionality.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Broadcast64(ulong value, int threadIndex)
    {
        ref var sharedMemory = ref Group.GetSharedMemory<ulong>();
        if (Group.Index == threadIndex)
            sharedMemory = value;
        Group.Barrier();

        return sharedMemory;
    }

    #endregion

    #region Reduce

    /// <summary>
    /// Implements a block-wide reduction algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>All lanes in the first warp contain the reduced value.</returns>
    public static T Reduce<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        AllReduce<T, TReduction>(value);

    /// <summary>
    /// Implements a block-wide reduction algorithm using a simple shared memory-bank
    /// based atomic reduction logic.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>All threads in the whole group contain the reduced value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AllReduce<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        AllReduceMemBanks<T, TReduction>(value);

    /// <summary>
    /// Implements a block-wide reduction algorithm using a simple shared memory-bank
    /// based atomic reduction logic.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>All threads in the whole group contain the reduced value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T AllReduceMemBanks<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T>
    {
        // A fixed number of memory banks to distribute the workload
        // of atomic operations in shared memory.
        const int NumMemoryBanks = 4;
        var sharedMemory = Group.GetSharedMemory<T>(NumMemoryBanks);

        var warpIdx = Warp.Index;
        var laneIdx = Warp.LaneIndex;

        if (warpIdx == 0)
        {
            for (int idx = laneIdx; idx < NumMemoryBanks; idx += Warp.Dimension)
                sharedMemory[idx] = TReduction.Identity;
        }
        Group.Barrier();

        var firstLaneValue = Warp.Reduce<T, TReduction>(value);
        if (Warp.IsFirstLane)
        {
            TReduction.AtomicApply(
                ref sharedMemory[warpIdx % NumMemoryBanks],
                firstLaneValue.Value);
        }
        Group.Barrier();

        // Note that this is explicitly unrolled (see NumMemoryBanks above)
        var result = sharedMemory[0];
        result = TReduction.Apply(result, sharedMemory[1]);
        result = TReduction.Apply(result, sharedMemory[2]);
        result = TReduction.Apply(result, sharedMemory[3]);
        Group.Barrier();

        return result;
    }

    #endregion

    #region Scan Primitives

    /// <summary>
    /// An abstract scan implementation that works on arbitrary types.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The actual scan operation type.</typeparam>
    private interface IScanImplementation<T, TScanOperation>
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T>
    {
        /// <summary>
        /// Performs a scan using the provided value from every thread.
        /// </summary>
        /// <param name="value">The value to scan.</param>
        /// <returns>The scanned value.</returns>
        static abstract T Scan(T value);

        /// <summary>
        /// Scans the right boundary value.
        /// </summary>
        /// <param name="boundaryValue">The current boundary value.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The scanned boundary value.</returns>
        static abstract T ScanRightBoundary(T boundaryValue, T value);
    }

    /// <summary>
    /// Represents an inclusive scan implementation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The actual scan operation type.</typeparam>
    private readonly struct InclusiveScanImplementation<T, TScanOperation>
        : IScanImplementation<T, TScanOperation>
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T>
    {
        /// <summary cref="IScanImplementation{T, TScanOperation}.Scan(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Scan(T value) =>
            Warp.InclusiveScan<T, TScanOperation>(value);

        /// <summary cref="IScanImplementation{T, TScanOperation}.ScanRightBoundary(
        /// T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ScanRightBoundary(T boundaryValue, T value) => boundaryValue;
    }

    /// <summary>
    /// Represents an exclusive scan implementation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The actual scan operation type.</typeparam>
    private readonly struct ExclusiveScanImplementation<T, TScanOperation>
        : IScanImplementation<T, TScanOperation>
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T>
    {
        /// <summary cref="IScanImplementation{T, TScanOperation}.Scan(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Scan(T value) =>
            Warp.ExclusiveScan<T, TScanOperation>(value);

        /// <summary cref="IScanImplementation{T, TScanOperation}.ScanRightBoundary(
        /// T, T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ScanRightBoundary(T boundaryValue, T value) =>
            TScanOperation.Apply(boundaryValue, value);
    }

    /// <summary>
    /// The internal intrinsic implementation to realize a single
    /// scan computation in the scope of a single group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The actual scan operation type.</typeparam>
    /// <typeparam name="TScanImplementation">The implementation type.</typeparam>
    /// <param name="value">The current value.</param>
    /// <param name="sharedMemory">The resulting shared memory.</param>
    /// <returns>The scanned value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ComputeScan<T, TScanOperation, TScanImplementation>(
        T value,
        out ArrayView<T> sharedMemory)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T>
        where TScanImplementation : struct, IScanImplementation<T, TScanOperation>
    {
        sharedMemory = Group.GetSharedMemory<T>(Warp.Dimension);

        int warpIdx = Warp.Index;
        // Initialize
        if (Group.Dimension / Warp.Dimension < Warp.Dimension)
        {
            if (warpIdx < 1)
                sharedMemory[Group.Index] = TScanOperation.Identity;
            Group.Barrier();
        }

        var scannedValue = TScanImplementation.Scan(value);
        if (Warp.IsLastLane)
        {
            sharedMemory[warpIdx] = TScanImplementation.ScanRightBoundary(
                scannedValue,
                value);
        }
        Group.Barrier();

        // Reduce results again in the first warp
        if (warpIdx < 1)
        {
            ref T sharedBoundary = ref sharedMemory[Group.Index];
            sharedBoundary = Warp.InclusiveScan<T, TScanOperation>(sharedBoundary);
        }
        Group.Barrier();

        T leftBoundary = warpIdx < 1
            ? TScanOperation.Identity
            : sharedMemory[warpIdx - 1];
        return TScanOperation.Apply(leftBoundary, scannedValue);
    }

    /// <summary>
    /// Performs a local scan operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The scan operation.</typeparam>
    /// <typeparam name="TScanImplementation">The scan implementation.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The scanned value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T PerformScan<T, TScanOperation, TScanImplementation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T>
        where TScanImplementation : struct, IScanImplementation<T, TScanOperation>
    {
        var result = ComputeScan<T, TScanOperation, TScanImplementation>(
            value,
            out var _);
        Group.Barrier();
        return result;
    }

    /// <summary>
    /// Performs a local scan operation including boundaries.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The scan operation.</typeparam>
    /// <typeparam name="TScanImplementation">The scan implementation.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <param name="boundaries">The resolved boundaries</param>
    /// <returns>The scanned value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T PerformScan<T, TScanOperation, TScanImplementation>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T>
        where TScanImplementation : struct, IScanImplementation<T, TScanOperation>
    {
        var result = ComputeScan<T, TScanOperation, TScanImplementation>(
            value,
            out var sharedMemory);
        boundaries = new ScanBoundaries<T>(
            sharedMemory[0],
            sharedMemory[Warp.Dimension - 1]);
        Group.Barrier();
        return result;
    }

    #endregion

    #region Scan Implementations

    /// <summary cref="Group.ExclusiveScan{T, TScanOperation}(T)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ExclusiveScan<T, TScanOperation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        PerformScan<
            T,
            TScanOperation,
            ExclusiveScanImplementation<T, TScanOperation>>(
                value);

    /// <summary cref="Group.InclusiveScan{T, TScanOperation}(T)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T InclusiveScan<T, TScanOperation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        PerformScan<
            T,
            TScanOperation,
            InclusiveScanImplementation<T, TScanOperation>>(
                value);

    /// <summary cref="Group.ExclusiveScan{T, TScan}(T, out ScanBoundaries{T})"/>
    /// T, out ScanBoundaries{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ExclusiveScan<T, TScanOperation>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        PerformScan<
            T,
            TScanOperation,
            ExclusiveScanImplementation<T, TScanOperation>>(
                value,
                out boundaries);

    /// <summary cref="Group.InclusiveScan{T, TScan}(T, out ScanBoundaries{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T InclusiveScan<T, TScanOperation>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        PerformScan<
            T,
            TScanOperation,
            InclusiveScanImplementation<T, TScanOperation>>(
                value,
                out boundaries);

    #endregion

    #region Sort

    /// <summary>
    /// Performs a group-wide radix sort pass.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">The radix sort operation.</typeparam>
    /// <param name="value">The original value in the current lane.</param>
    /// <returns>The sorted value in the current lane.</returns>
    public static T RadixSort<T, TRadixSortOperation>(T value)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T>
    {
        var sharedArray = Group.GetSharedMemoryPerThread<T>();
        var keys0 = Group.GetSharedMemoryPerWarp<int>();
        var keys1 = Group.GetSharedMemoryPerWarp<int>();
        ref var bitChangePosition = ref Group.GetSharedMemory<int>();

        int i = Group.Index;
        sharedArray[i] = value;
        Group.Barrier();

        for (int bitIdx = 0; bitIdx < TRadixSortOperation.NumBits; ++bitIdx)
        {
            if (Warp.IsFirstWarp)
            {
                keys0[i] = 0;
                keys1[i] = 0;
            }
            Group.Barrier();

            var element = sharedArray[i];
            int key = TRadixSortOperation.ExtractRadixBits(element, bitIdx, 1);
            int key0 = key == 0 ? 1 : 0;
            int key1 = 1 - key0;

            for (int offset = 1; offset < Warp.Dimension - 1; offset <<= 1)
            {
                var partialKey0 = Warp.ShuffleUp(key0, offset);
                var partialKey1 = Warp.ShuffleUp(key1, offset);
                key0 += Utilities.Select(Warp.LaneIndex >= offset, partialKey0, 0);
                key1 += Utilities.Select(Warp.LaneIndex >= offset, partialKey1, 0);
            }
            if (Warp.IsLastLane)
            {
                keys0[Warp.Index] = key0;
                keys1[Warp.Index] = key1;
            }
            Group.Barrier();

            if (Warp.Index == 0)
            {
                var globalKey0 = keys0[Warp.LaneIndex];
                var globalKey1 = keys1[Warp.LaneIndex];

                for (int offset = 1; offset < Warp.Dimension - 1; offset <<= 1)
                {
                    var partialKey0 = Warp.ShuffleUp(globalKey0, offset);
                    var partialKey1 = Warp.ShuffleUp(globalKey1, offset);
                    globalKey0 += Utilities.Select(
                        Warp.LaneIndex >= offset,
                        partialKey0,
                        0);
                    globalKey1 += Utilities.Select(
                        Warp.LaneIndex >= offset,
                        partialKey1,
                        0);
                }

                if (Warp.IsLastLane)
                    bitChangePosition = globalKey0;
                Warp.Barrier();

                globalKey0 = Warp.ShuffleUp(globalKey0, 1);
                globalKey1 = Warp.ShuffleUp(globalKey1, 1);
                keys0[Warp.LaneIndex] = globalKey0;
                keys1[Warp.LaneIndex] = globalKey1;

                if (Warp.IsFirstLane)
                {
                    keys0[0] = 0;
                    keys1[0] = 0;
                }
            }
            Group.Barrier();

            var target = key == 0 ?
                keys0[Warp.Index] + key0 - 1 :
                bitChangePosition + keys1[Warp.Index] + key1 - 1;
            sharedArray[target] = element;
            Group.Barrier();
        }

        return sharedArray[i];
    }

    #endregion
}
