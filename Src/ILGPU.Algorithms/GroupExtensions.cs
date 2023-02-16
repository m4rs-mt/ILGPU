// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.IL;
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.IR.Intrinsics;
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
    }
}
