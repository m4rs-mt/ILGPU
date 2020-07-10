// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: PTXWarpExtensions.cs
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
    static class PTXWarpExtensions
    {
        #region Reduce

        /// <summary cref="WarpExtensions.Reduce{T, TReduction}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Reduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T>
        {
            TReduction reduction = default;
            for (int laneOffset = Warp.WarpSize / 2; laneOffset > 0; laneOffset >>= 1)
            {
                var shuffled = Warp.ShuffleDown(value, laneOffset);
                value = reduction.Apply(value, shuffled);
            }
            return value;
        }

        /// <summary cref="WarpExtensions.AllReduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T>
        {
            TReduction reduction = default;
            for (int laneMask = Warp.WarpSize / 2; laneMask > 0; laneMask >>= 1)
            {
                var shuffled = Warp.ShuffleXor(value, laneMask);
                value = reduction.Apply(value, shuffled);
            }
            return value;
        }

        #endregion

        #region Scan

        /// <summary cref="WarpExtensions.ExclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var inclusive = InclusiveScan<T, TScanOperation>(value);

            var exclusive = Warp.ShuffleUp(inclusive, 1);
            return Warp.IsFirstLane ? default : exclusive;
        }

        /// <summary cref="WarpExtensions.InclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            TScanOperation warpScan = default;

            var laneIdx = Warp.LaneIdx;
            for (int delta = 1; delta < Warp.WarpSize; delta <<= 1)
            {
                var otherValue = Warp.ShuffleUp(value, delta);
                if (laneIdx >= delta)
                    value = warpScan.Apply(value, otherValue);
            }
            return value;
        }

        #endregion
    }
}
