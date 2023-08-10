// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.IL;
using ILGPU.Algorithms.RadixSortOperations;
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.IR.Intrinsics;
using ILGPU.Util;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Contains extension methods for warps.
    /// </summary>
    public static class WarpExtensions
    {
        #region Reduce

        /// <summary>
        /// Performs a warp-wide reduction.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <returns>The first lane (lane id = 0) will return reduced result.</returns>
        [IntrinsicImplementation]
        public static T Reduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T> =>
            ILWarpExtensions.Reduce<T, TReduction>(value);

        /// <summary>
        /// Performs a warp-wide reduction.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <returns>All lanes will return the reduced result.</returns>
        [IntrinsicImplementation]
        public static T AllReduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T> =>
            ILWarpExtensions.AllReduce<T, TReduction>(value);

        #endregion

        #region Scan

        /// <summary>
        /// Performs a warp-wide exclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [IntrinsicImplementation]
        public static T ExclusiveScan<T, TScan>(T value)
            where T : unmanaged
            where TScan : struct, IScanReduceOperation<T> =>
            ILWarpExtensions.ExclusiveScan<T, TScan>(value);

        /// <summary>
        /// Performs a warp-wide inclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [IntrinsicImplementation]
        public static T InclusiveScan<T, TScan>(T value)
            where T : unmanaged
            where TScan : struct, IScanReduceOperation<T> =>
            ILWarpExtensions.InclusiveScan<T, TScan>(value);

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
            TRadixSortOperation operation = default;
            for (int bitIdx = 0; bitIdx < operation.NumBits; bitIdx++)
            {
                var key = operation.ExtractRadixBits(value, bitIdx, 1);
                var key0 = key == 0 ? 1 : 0;
                var key1 = 1 - key0;

                for (int offset = 1; offset < Warp.WarpSize - 1; offset <<= 1)
                {
                    var partialKey0 = Warp.ShuffleUp(key0, offset);
                    var partialKey1 = Warp.ShuffleUp(key1, offset);
                    key0 += Utilities.Select(Warp.LaneIdx >= offset, partialKey0, 0);
                    key1 += Utilities.Select(Warp.LaneIdx >= offset, partialKey1, 0);
                }
                key1 += Warp.Shuffle(key0, Warp.WarpSize - 1);

                var target = key == 0 ? key0 - 1 : key1 - 1;
                T newElement = operation.DefaultValue;
                for (int k = 0; k < Warp.WarpSize; k++)
                {
                    var targetLane = Warp.Shuffle(target, k);
                    var retrievedElement = Warp.Shuffle(value, k);
                    if (targetLane == Warp.LaneIdx)
                        newElement = retrievedElement;
                }

                value = newElement;
            }
            return value;
        }

        #endregion
    }
}
