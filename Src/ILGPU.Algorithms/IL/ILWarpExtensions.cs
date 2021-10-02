// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: ILWarpExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using System.Runtime.CompilerServices;
using static ILGPU.Algorithms.IL.ILFunctions;

namespace ILGPU.Algorithms.IL
{
    /// <summary>
    /// Custom IL-specific implementations.
    /// </summary>
    static class ILWarpExtensions
    {
        #region Nested Types

        /// <summary>
        /// Implements ILFunctions for warps.
        /// </summary>
        private readonly struct WarpImplementation : IILFunctionImplementation
        {
            /// <summary>
            /// Returns 256.
            /// </summary>
            /// <remarks>
            /// TODO: refine the implementation to avoid a hard-coded constant.
            /// </remarks>
            public readonly int MaxNumThreads => 256;

            /// <summary>
            /// Returns true if this is the first warp thread.
            /// </summary>
            public readonly bool IsFirstThread => Warp.IsFirstLane;

            /// <summary>
            /// Returns current lane index.
            /// </summary>
            public readonly int ThreadIndex => Warp.LaneIdx;

            /// <summary>
            /// Returns the warp size.
            /// </summary>
            public readonly int ThreadDimension => Warp.WarpSize;

            /// <summary>
            /// Returns the number of warps per group.
            /// </summary>
            public readonly int ReduceSegments => MaxNumThreads / Warp.WarpSize;

            /// <summary>
            /// Returns the current warp index.
            /// </summary>
            public readonly int ReduceSegmentIndex => Warp.WarpIdx;

            /// <summary>
            /// Performs a warp-wide barrier.
            /// </summary>
            public readonly void Barrier() => Warp.Barrier();
        }

        #endregion

        #region Reduce

        /// <summary cref="WarpExtensions.Reduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Reduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T> =>
            AllReduce<T, TReduction>(value);

        /// <summary cref="WarpExtensions.AllReduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T> =>
            AllReduce<T, TReduction, WarpImplementation>(value);

        #endregion

        #region Scan

        /// <summary cref="WarpExtensions.ExclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            ExclusiveScan<T, TScanOperation, WarpImplementation>(value);

        /// <summary cref="WarpExtensions.InclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            InclusiveScan<T, TScanOperation, WarpImplementation>(value);

        #endregion
    }
}
