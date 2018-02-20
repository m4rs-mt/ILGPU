// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: GroupExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.ReductionOperations;
using ILGPU.ShuffleOperations;
using System.Diagnostics;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Contains extension methods for thread groups.
    /// </summary>
    public static class GroupExtensions
    {
        #region Reduce

        /// <summary>
        /// Implements a basic block-wide reduction algorithm.
        /// The algorithm is based on the one from https://devblogs.nvidia.com/parallelforall/faster-parallel-reductions-kepler/.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="groupThreadIdx">The current group-thread index.</param>
        /// <param name="value">The current value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <param name="sharedMemory">A view to a section of group-shared memory.</param>
        /// <returns>The reduced value.</returns>
        public static T Reduce<T, TShuffleDown, TReduction>(
            Index groupThreadIdx,
            T value,
            TShuffleDown shuffleDown,
            TReduction reduction,
            ArrayView<T> sharedMemory)
            where T : struct
            where TShuffleDown : IShuffleDown<T>
            where TReduction : IReduction<T>
        {
            Debug.Assert(Warp.WarpSize > 1, "This algorithm can only be used on architectures with a warp size > 1");

            var warpIdx = Warp.ComputeWarpIdx(groupThreadIdx);
            var laneIdx = Warp.LaneIdx;

            value = Warp.Reduce(value, shuffleDown, reduction);

            if (laneIdx == 0)
            {
                Debug.Assert(warpIdx < sharedMemory.Length, "Shared memory out of range");
                sharedMemory[warpIdx] = value;
            }

            Group.Barrier();

            if (groupThreadIdx < Group.Dimension.X / Warp.WarpSize)
                value = sharedMemory[laneIdx];
            else
                value = reduction.NeutralElement;

            if (warpIdx == 0)
                value = Warp.Reduce(value, shuffleDown, reduction);

            return value;
        }

        #endregion
    }
}
