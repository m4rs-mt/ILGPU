// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: ILGroupExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
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
            where TReduction : IScanReduceOperation<T> =>
            AllReduce<T, TReduction>(value);

        /// <summary cref="GroupExtensions.AllReduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T>
        {
            ref var sharedMemory = ref SharedMemory.Allocate<T>();

            TReduction reduction = default;
            if (Group.IsFirstThread)
                sharedMemory = reduction.Identity;
            Group.Barrier();

            reduction.AtomicApply(ref sharedMemory, value);

            Group.Barrier();
            return sharedMemory;
        }

        #endregion

        #region Scan

        /// <summary>
        /// The maximum number of supported thread per group on the
        /// CPU accelerator for the scan algorithms.
        /// </summary>
        internal const int MaxNumThreadsPerGroup = 64;

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
                sharedMemory[Math.Max(0, Group.DimX - 2)]);
            return Group.IsFirstThread
                ? default(TScanOperation).Identity
                : sharedMemory[Group.IdxX - 1];
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
            var sharedMemory = InclusiveScanImplementation<T, TScanOperation>(value);
            boundaries = new ScanBoundaries<T>(
                sharedMemory[0],
                sharedMemory[Group.DimX - 1]);
            return sharedMemory[Group.IdxX];
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
            // Load values into shared memory
            var sharedMemory = SharedMemory.Allocate<T>(MaxNumThreadsPerGroup);
            Debug.Assert(Group.DimX <= MaxNumThreadsPerGroup, "Invalid group size");
            sharedMemory[Group.IdxX] = value;
            Group.Barrier();

            // First thread performs all operations
            if (Group.IsFirstThread)
            {
                TScanOperation scanOperation = default;
                for (int i = 1; i < Group.DimX; ++i)
                    sharedMemory[i] = scanOperation.Apply(
                        sharedMemory[i - 1],
                        sharedMemory[i]);
            }
            Group.Barrier();

            return sharedMemory;
        }

        #endregion
    }
}
