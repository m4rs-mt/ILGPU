// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILGroupExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.IL
{
    /// <summary>
    /// Custom IL-specific implementations.
    /// </summary>
    static class ILGroupExtensions
    {
        #region Reduce

        /// <summary cref="GroupExtensions.Reduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Reduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T> =>
            AllReduce<T, TReduction>(value);

        /// <summary cref="GroupExtensions.AllReduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T>
        {
            TReduction reduction = default;

            ref var sharedMemory = ref SharedMemory.Allocate<T>();
            if (Group.IsFirstThread)
                sharedMemory = reduction.Identity;
            Group.Barrier();

            // Reduce inside all warps first
            var firstLaneReduced = ILWarpExtensions.Reduce<T, TReduction>(value);
            if (Warp.IsFirstLane)
                reduction.AtomicApply(ref sharedMemory, firstLaneReduced);

            Group.Barrier();
            return sharedMemory;
        }

        #endregion

        #region Scan

        /// <summary cref="GroupExtensions.ExclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ExclusiveScanWithBoundaries<T, TScanOperation>(value, out var _);

        /// <summary cref="GroupExtensions.InclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            InclusiveScanWithBoundaries<T, TScanOperation>(value, out var _);

        /// <summary cref="GroupExtensions.ExclusiveScanWithBoundaries{T, TScanOperation}(
        /// T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScanWithBoundaries<T, TScanOperation>(
            T value,
            out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var sharedMemory = InclusiveScanImplementation<T, TScanOperation>(value);
            boundaries = new ScanBoundaries<T>(
                sharedMemory[0],
                sharedMemory[Math.Max(0, Group.Dimension.Size - 2)]);
            return Group.IsFirstThread
                ? default(TScanOperation).Identity
                : sharedMemory[Group.LinearIndex - 1];
        }

        /// <summary cref="GroupExtensions.InclusiveScanWithBoundaries{T, TScanOperation}(
        /// T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScanWithBoundaries<T, TScanOperation>(
            T value,
            out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var sharedMemory = InclusiveScanImplementation<T, TScanOperation>(
                value);
            boundaries = new ScanBoundaries<T>(
                sharedMemory[0],
                sharedMemory[Group.Dimension.Size - 1]);
            return sharedMemory[Group.LinearIndex];
        }

        /// <summary>
        /// Performs a group-wide inclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ArrayView<T> InclusiveScanImplementation<T, TScanOperation>(
            T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            const int MaxNumThreads = 2048;

            // Load values into shared memory
            var sharedMemory = SharedMemory.Allocate<T>(MaxNumThreads);
            Debug.Assert(
                Group.Dimension.Size <= MaxNumThreads,
                "Invalid group/warp size");
            sharedMemory[Group.LinearIndex] = value;
            Group.Barrier();

            // First thread performs all operations
            if (Group.IsFirstThread)
            {
                TScanOperation scanOperation = default;
                for (int i = 1; i < Group.Dimension.Size; ++i)
                {
                    sharedMemory[i] = scanOperation.Apply(
                        sharedMemory[i - 1],
                        sharedMemory[i]);
                }
            }
            Group.Barrier();

            return sharedMemory;
        }

        /// <summary>
        /// Prepares for the next iteration of a group-wide exclusive scan within the
        /// same kernel.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="leftBoundary">The left boundary value.</param>
        /// <param name="rightBoundary">The right boundary value.</param>
        /// <param name="currentValue">The current value.</param>
        /// <returns>The starting value for the next iteration.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScanNextIteration<T, TScanOperation>(
            T leftBoundary,
            T rightBoundary,
            T currentValue)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var scanOperation = default(TScanOperation);
            var nextBoundary = scanOperation.Apply(leftBoundary, rightBoundary);
            return scanOperation.Apply(
                nextBoundary,
                Group.Broadcast(currentValue, Group.DimX - 1));
        }

        /// <summary>
        /// Prepares for the next iteration of a group-wide inclusive scan within the
        /// same kernel.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="leftBoundary">The left boundary value.</param>
        /// <param name="rightBoundary">The right boundary value.</param>
        /// <param name="currentValue">The current value.</param>
        /// <returns>The starting value for the next iteration.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScanNextIteration<T, TScanOperation>(
            T leftBoundary,
            T rightBoundary,
            T currentValue)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var scanOperation = default(TScanOperation);
            return scanOperation.Apply(leftBoundary, rightBoundary);
        }

        #endregion
    }
}
