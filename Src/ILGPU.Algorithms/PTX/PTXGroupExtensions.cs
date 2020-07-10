// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: PTXGroupExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.PTX
{
    /// <summary>
    /// Custom PTX-specific implementations.
    /// </summary>
    static class PTXGroupExtensions
    {
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
            // A fixed number of memory banks to distribute the workload
            // of the atomic operations in shared memory.
            const int NumMemoryBanks = 4;
            var sharedMemory = SharedMemory.Allocate<T>(NumMemoryBanks);

            var warpIdx = Warp.ComputeWarpIdx(Group.IdxX);
            var laneIdx = Warp.LaneIdx;

            TReduction reduction = default;

            if (warpIdx == 0)
            {
                for (int bankIdx = laneIdx; bankIdx < NumMemoryBanks; bankIdx += Warp.WarpSize)
                    sharedMemory[bankIdx] = reduction.Identity;
            }
            Group.Barrier();

            value = PTXWarpExtensions.Reduce<T, TReduction>(value);
            if (laneIdx == 0)
                reduction.AtomicApply(ref sharedMemory[warpIdx % NumMemoryBanks], value);
            Group.Barrier();

            // Note that this is explicitly unrolled (see NumMemoryBanks above)
            var result = sharedMemory[0];
            result = reduction.Apply(result, sharedMemory[1]);
            result = reduction.Apply(result, sharedMemory[2]);
            result = reduction.Apply(result, sharedMemory[3]);
            Group.Barrier();

            return result;
        }

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
            T Scan(T value);

            /// <summary>
            /// Scans the right boundary value.
            /// </summary>
            /// <param name="boundaryValue">The current boundary value.</param>
            /// <param name="value">The value to add.</param>
            /// <returns>The scanned boundary value.</returns>
            T ScanRightBoundary(T boundaryValue, T value);

            /// <summary>
            /// Loads the i-th value from the given view.
            /// </summary>
            /// <param name="warpIdx">The warp index.</param>
            /// <param name="values">The values to load from.</param>
            /// <returns>The loaded value.</returns>
            T Load(int warpIdx, ArrayView<T> values);
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
            public T Scan(T value) =>
                PTXWarpExtensions.InclusiveScan<T, TScanOperation>(value);

            /// <summary cref="IScanImplementation{T, TScanOperation}.ScanRightBoundary(T, T)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T ScanRightBoundary(T boundaryValue, T value) => boundaryValue;

            /// <summary cref="IScanImplementation{T, TScanOperation}.Load(int, ArrayView{T})"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Load(int warpIdx, ArrayView<T> values)
            {
                TScanOperation scanOperation = default;
                if (warpIdx < 1)
                    return scanOperation.Identity;
                return values[warpIdx];
            }
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
            public T Scan(T value) =>
                PTXWarpExtensions.ExclusiveScan<T, TScanOperation>(value);

            /// <summary cref="IScanImplementation{T, TScanOperation}.ScanRightBoundary(T, T)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T ScanRightBoundary(T boundaryValue, T value)
            {
                TScanOperation scanOperation = default;
                return scanOperation.Apply(boundaryValue, value);
            }

            /// <summary cref="IScanImplementation{T, TScanOperation}.Load(int, ArrayView{T})"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Load(int warpIdx, ArrayView<T> values) =>
                values[warpIdx];
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
            const int SharedMemoryLength = 32;
            sharedMemory = SharedMemory.Allocate<T>(SharedMemoryLength);

            int warpIdx = Warp.WarpIdx;

            TScanOperation scanOperation = default;

            // Initialize
            if (Group.DimX / Warp.WarpSize < SharedMemoryLength)
            {
                if (warpIdx < 1)
                    sharedMemory[Group.IdxX] = scanOperation.Identity;
                Group.Barrier();
            }

            TScanImplementation scanImplementation = default;
            var scannedValue = scanImplementation.Scan(value);
            if (Warp.IsLastLane)
                sharedMemory[warpIdx] = scanImplementation.ScanRightBoundary(scannedValue, value);
            Group.Barrier();

            // Reduce results again in the first warp
            if (warpIdx < 1)
            {
                ref T sharedBoundary = ref sharedMemory[Group.IdxX];
                sharedBoundary = PTXWarpExtensions.InclusiveScan<T, TScanOperation>(sharedBoundary);
            }
            Group.Barrier();

            T leftBoundary = warpIdx < 1 ? scanOperation.Identity : sharedMemory[warpIdx - 1];
            return scanOperation.Apply(leftBoundary, scannedValue);
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
            var result = ComputeScan<T, TScanOperation, TScanImplementation>(value, out var _);
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
            boundaries = new ScanBoundaries<T>(sharedMemory[0], sharedMemory[Warp.WarpSize - 1]);
            Group.Barrier();
            return result;
        }

        #endregion

        #region Scan Implementations

        /// <summary cref="GroupExtensions.ExclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            PerformScan<T, TScanOperation, ExclusiveScanImplementation<T, TScanOperation>>(
                value);

        /// <summary cref="GroupExtensions.InclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            PerformScan<T, TScanOperation, InclusiveScanImplementation<T, TScanOperation>>(
                value);

        /// <summary cref="GroupExtensions.ExclusiveScanWithBoundaries{T, TScanOperation}(T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScanWithBoundaries<T, TScanOperation>(T value, out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            PerformScan<T, TScanOperation, ExclusiveScanImplementation<T, TScanOperation>>(
                value,
                out boundaries);

        /// <summary cref="GroupExtensions.InclusiveScanWithBoundaries{T, TScanOperation}(T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScanWithBoundaries<T, TScanOperation>(T value, out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            PerformScan<T, TScanOperation, InclusiveScanImplementation<T, TScanOperation>>(
                value,
                out boundaries);

        #endregion
    }
}
