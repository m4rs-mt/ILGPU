// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILGroupExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using System.Runtime.CompilerServices;
using static ILGPU.Algorithms.IL.ILFunctions;

namespace ILGPU.Algorithms.IL
{
    /// <summary>
    /// Custom IL-specific implementations.
    /// </summary>
    static class ILGroupExtensions
    {
        #region Nested Types

        /// <summary>
        /// Implements ILFunctions for groups.
        /// </summary>
        private readonly struct GroupImplementation : IILFunctionImplementation
        {
            /// <summary>
            /// Returns 1024.
            /// </summary>
            /// <remarks>
            /// TODO: refine the implementation to avoid a hard-coded constant.
            /// </remarks>
            public readonly int MaxNumThreads => 1024;

            /// <summary>
            /// Returns true if this is the first group thread.
            /// </summary>
            public readonly bool IsFirstThread => Group.IsFirstThread;

            /// <summary>
            /// Returns current linear group index.
            /// </summary>
            public readonly int ThreadIndex => Group.LinearIndex;

            /// <summary>
            /// Returns the linear group dimension.
            /// </summary>
            public readonly int ThreadDimension => Group.Dimension.Size;

            /// <summary>
            /// Returns 1.
            /// </summary>
            public readonly int ReduceSegments => 1;

            /// <summary>
            /// Returns 0.
            /// </summary>
            public readonly int ReduceSegmentIndex => 0;

            /// <summary>
            /// Performs a group-wide barrier.
            /// </summary>
            public readonly void Barrier() => Group.Barrier();
        }

        #endregion

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
            where TReduction : IScanReduceOperation<T> =>
            AllReduce<T, TReduction, GroupImplementation>(value);

        #endregion

        #region Scan

        /// <summary cref="GroupExtensions.ExclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ExclusiveScan<T, TScanOperation, GroupImplementation>(value);

        /// <summary cref="GroupExtensions.InclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            InclusiveScan<T, TScanOperation, GroupImplementation>(value);

        /// <summary cref="GroupExtensions.ExclusiveScanWithBoundaries{T, TScanOperation}(
        /// T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScanWithBoundaries<T, TScanOperation>(
            T value,
            out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ExclusiveScanWithBoundaries<T, TScanOperation, GroupImplementation>(
                value,
                out boundaries);

        /// <summary cref="GroupExtensions.InclusiveScanWithBoundaries{T, TScanOperation}(
        /// T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScanWithBoundaries<T, TScanOperation>(
            T value,
            out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            InclusiveScanWithBoundaries<T, TScanOperation, GroupImplementation>(
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
