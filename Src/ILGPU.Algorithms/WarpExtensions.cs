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
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.IR.Intrinsics;

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
    }
}
