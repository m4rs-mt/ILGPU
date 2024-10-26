// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.IL;
using ILGPU.Algorithms.RadixSortOperations;
using ILGPU.Algorithms.Random;
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.IR.Intrinsics;
using ILGPU.Runtime;
using ILGPU.Util;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Contains extension methods for thread groups.
    /// </summary>
    public static class GroupExtensions
    {
        #region Reduce

        /// <summary>
        /// Implements a block-wide reduction algorithm.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <returns>All lanes in the first warp contain the reduced value.</returns>
        [IntrinsicImplementation]
        public static T Reduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T> =>
            ILGroupExtensions.Reduce<T, TReduction>(value);

        /// <summary>
        /// Implements a block-wide reduction algorithm.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <returns>All threads in the whole group contain the reduced value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static T AllReduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T> =>
            ILGroupExtensions.AllReduce<T, TReduction>(value);

        #endregion

        #region Scan

        /// <summary>
        /// Performs a group-wide exclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static T ExclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ILGroupExtensions.ExclusiveScan<T, TScanOperation>(value);

        /// <summary>
        /// Performs a group-wide inclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static T InclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ILGroupExtensions.InclusiveScan<T, TScanOperation>(value);

        /// <summary>
        /// Performs a group-wide exclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <param name="boundaries">The scan boundaries.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static T ExclusiveScanWithBoundaries<T, TScanOperation>(
            T value,
            out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ILGroupExtensions.ExclusiveScanWithBoundaries<T, TScanOperation>(
                value,
                out boundaries);

        /// <summary>
        /// Performs a group-wide inclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <param name="boundaries">The scan boundaries.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [IntrinsicImplementation]
        public static T InclusiveScanWithBoundaries<T, TScanOperation>(
            T value,
            out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ILGroupExtensions.InclusiveScanWithBoundaries<T, TScanOperation>(
                value,
                out boundaries);

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
        [IntrinsicImplementation]
        public static T ExclusiveScanNextIteration<T, TScanOperation>(
            T leftBoundary,
            T rightBoundary,
            T currentValue)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ILGroupExtensions.ExclusiveScanNextIteration<T, TScanOperation>(
                leftBoundary,
                rightBoundary,
                currentValue);

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
        [IntrinsicImplementation]
        public static T InclusiveScanNextIteration<T, TScanOperation>(
            T leftBoundary,
            T rightBoundary,
            T currentValue)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ILGroupExtensions.InclusiveScanNextIteration<T, TScanOperation>(
                leftBoundary,
                rightBoundary,
                currentValue);

        #endregion

        #region Scan Wrappers

        /// <summary>
        /// Represents an abstract wrapper for scan operations.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        public interface IGroupScan<T, TScanOperation>
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            /// <summary>
            /// Performs a group-wide scan.
            /// </summary>
            /// <param name="value">The value to scan.</param>
            /// <returns>The resulting value for the current lane.</returns>
            T Scan(T value);

            /// <summary>
            /// Performs a group-wide scan.
            /// </summary>
            /// <param name="value">The value to scan.</param>
            /// <param name="boundaries">The scan boundaries.</param>
            /// <returns>The resulting value for the current lane.</returns>
            T Scan(T value, out ScanBoundaries<T> boundaries);
        }

        /// <summary>
        /// Represents a wrapper for an inclusive-scan operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        public readonly struct InclusiveGroupScan<T, TScanOperation> :
            IGroupScan<T, TScanOperation>
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            /// <summary>
            /// Performs a group-wide inclusive scan.
            /// </summary>
            /// <param name="value">The value to scan.</param>
            /// <returns>The resulting value for the current lane.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Scan(T value) => InclusiveScan<T, TScanOperation>(value);

            /// <summary>
            /// Performs a group-wide inclusive scan.
            /// </summary>
            /// <param name="value">The value to scan.</param>
            /// <param name="boundaries">The scan boundaries.</param>
            /// <returns>The resulting value for the current lane.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Scan(T value, out ScanBoundaries<T> boundaries) =>
                InclusiveScanWithBoundaries<T, TScanOperation>(value, out boundaries);
        }

        /// <summary>
        /// Represents a wrapper for an exclusive-scan operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        public readonly struct ExclusiveGroupScan<T, TScanOperation> :
            IGroupScan<T, TScanOperation>
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            /// <summary>
            /// Performs a group-wide inclusive scan.
            /// </summary>
            /// <param name="value">The value to scan.</param>
            /// <returns>The resulting value for the current lane.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Scan(T value) =>
                ExclusiveScan<T, TScanOperation>(value);

            /// <summary>
            /// Performs a group-wide inclusive scan.
            /// </summary>
            /// <param name="value">The value to scan.</param>
            /// <param name="boundaries">The scan boundaries.</param>
            /// <returns>The resulting value for the current lane.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Scan(T value, out ScanBoundaries<T> boundaries) =>
                ExclusiveScanWithBoundaries<T, TScanOperation>(value, out boundaries);
        }

        #endregion

        #region Sort

        /// <summary>
        /// Performs a group-wide radix sort pass.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TRadixSortOperation">The radix sort operation.</typeparam>
        /// <param name="value">The original value in the current lane.</param>
        /// <param name="sharedMemoryArray">The shared memory used for sorting.</param>
        /// <returns>The sorted value in the current lane.</returns>
        public static T RadixSort<T, TRadixSortOperation>(
            T value,
            ArrayView<byte> sharedMemoryArray)
            where T : unmanaged
            where TRadixSortOperation : struct, IRadixSortOperation<T>
        {
            var arrayLength = Interop.SizeOf<T>() * Group.DimX;
            var keyOffset = arrayLength + arrayLength % sizeof(int);
            var keyLength = Warp.WarpSize * sizeof(int);

            var sharedArray = sharedMemoryArray.SubView(0, arrayLength).Cast<T>();
            var keys0 = sharedMemoryArray.SubView(keyOffset, keyLength).Cast<int>();
            var keys1 = sharedMemoryArray.SubView(
                keyOffset + keyLength,
                keyLength).Cast<int>();
            ref var bitChangePosition = ref sharedMemoryArray.SubView(
                keyOffset + 2 * keyLength,
                sizeof(int)).Cast<int>()[0];

            int i = Group.IdxX;
            sharedArray[i] = value;
            Group.Barrier();
            TRadixSortOperation operation = default;

            for (int bitIdx = 0; bitIdx < operation.NumBits; bitIdx++)
            {
                if (Warp.WarpIdx < 1)
                {
                    keys0[i] = 0;
                    keys1[i] = 0;
                }
                Group.Barrier();
                var element = sharedArray[i];
                int key = operation.ExtractRadixBits(element, bitIdx, 1);
                int key0 = key == 0 ? 1 : 0;
                int key1 = 1 - key0;

                for (int offset = 1; offset < Warp.WarpSize - 1; offset <<= 1)
                {
                    var partialKey0 = Warp.ShuffleUp(key0, offset);
                    var partialKey1 = Warp.ShuffleUp(key1, offset);
                    key0 += Utilities.Select(Warp.LaneIdx >= offset, partialKey0, 0);
                    key1 += Utilities.Select(Warp.LaneIdx >= offset, partialKey1, 0);
                }
                if (Warp.IsLastLane)
                {
                    keys0[Warp.WarpIdx] = key0;
                    keys1[Warp.WarpIdx] = key1;
                }
                Group.Barrier();

                if (Warp.WarpIdx == 0)
                {
                    var globalKey0 = keys0[Warp.LaneIdx];
                    var globalKey1 = keys1[Warp.LaneIdx];

                    for (int offset = 1; offset < Warp.WarpSize - 1; offset <<= 1)
                    {
                        var partialKey0 = Warp.ShuffleUp(globalKey0, offset);
                        var partialKey1 = Warp.ShuffleUp(globalKey1, offset);
                        globalKey0 += Utilities.Select(
                            Warp.LaneIdx >= offset,
                            partialKey0,
                            0);
                        globalKey1 += Utilities.Select(
                            Warp.LaneIdx >= offset,
                            partialKey1,
                            0);
                    }

                    if (Warp.IsLastLane)
                        bitChangePosition = globalKey0;
                    Warp.Barrier();

                    globalKey0 = Warp.ShuffleUp(globalKey0, 1);
                    globalKey1 = Warp.ShuffleUp(globalKey1, 1);
                    keys0[Warp.LaneIdx] = globalKey0;
                    keys1[Warp.LaneIdx] = globalKey1;

                    if (Warp.IsFirstLane)
                    {
                        keys0[0] = 0;
                        keys1[0] = 0;
                    }
                }
                Group.Barrier();

                var target = key == 0 ?
                    keys0[Warp.WarpIdx] + key0 - 1 :
                    bitChangePosition + keys1[Warp.WarpIdx] + key1 - 1;
                sharedArray[target] = element;
                Group.Barrier();
            }

            return sharedArray[i];
        }

        #endregion

        #region Permute

        /// <summary>
        /// Permutes the given value within a warp.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TRandomProvider">
        /// The random number provider type.
        /// </typeparam>
        /// <param name="value">The value to permute.</param>
        /// <param name="sharedMemoryArray">The shared memory used for sorting.</param>
        /// <param name="rngView">The random number generator.</param>
        /// <returns>A permuted value from another random group index.</returns>
        public static T Permute<T, TRandomProvider>(
            T value,
            ArrayView<byte> sharedMemoryArray,
            RNGView<TRandomProvider> rngView)
            where T : unmanaged
            where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
        {
            var arrayLength = Interop.SizeOf<T>() * Group.DimX;
            var radixMemory = arrayLength + arrayLength % sizeof(int);
            var sharedArray = sharedMemoryArray.SubView(0, arrayLength).Cast<T>();

            sharedArray[Group.IdxX] = value;
            Group.Barrier();

            int lane = (rngView.Next() & 0x7ffffc00) + Group.IdxX;
            lane = GroupExtensions.RadixSort<int, AscendingInt32>(
                lane,
                sharedMemoryArray.SubView(radixMemory));
            return sharedArray[lane & 0x000003ff];
        }

        #endregion
    }
}
