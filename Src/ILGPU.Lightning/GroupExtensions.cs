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
using System.Runtime.CompilerServices;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Contains extension methods for thread groups.
    /// </summary>
    public static class GroupExtensions
    {
        #region Reduce

        /// <summary>
        /// A generic result handler interface that performs the final group-wide reduction step
        /// in the scope of a <see cref="Reduce{T, TShuffleDown, TReduction, TReductionResultHandler}(Index, T, TShuffleDown, TReduction)"/>
        /// function.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        public interface IReductionResultHandler<T, TReduction>
            where T : struct
            where TReduction : IAtomicReduction<T>
        {
            /// <summary>
            /// Performs a final reduction step within a group.
            /// </summary>
            /// <param name="warpIdx">The current warp index within a group.</param>
            /// <param name="laneIdx">The current lane index within the current warp.</param>
            /// <param name="sharedMemory">A view to shared memory temporaries.</param>
            /// <param name="reduction">The current reduction logic.</param>
            /// <returns>The reduced value.</returns>
            T Reduce(
                Index warpIdx,
                Index laneIdx,
                ArrayView<T> sharedMemory,
                TReduction reduction);
        }

        /// <summary>
        /// A result handler that propagates the reduced value to every thread in the whole group.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        public readonly struct AllReduceResultHandler<T, TReduction> : IReductionResultHandler<T, TReduction>
            where T : struct
            where TReduction : IAtomicReduction<T>
        {
            /// <summary cref="IReductionResultHandler{T, TReduction}.Reduce(Index, Index, ArrayView{T}, TReduction)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Reduce(
                Index warpIdx,
                Index laneIdx,
                ArrayView<T> sharedMemory,
                TReduction reduction)
            {
                var result = sharedMemory[0];
                for (int i = 1, e = sharedMemory.Length; i < e; ++i)
                    result = reduction.Reduce(result, sharedMemory[i]);
                return result;
            }
        }

        /// <summary>
        /// A result handler that propagates the reduced value to every thread in first warp.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        public readonly struct FirstWarpReduceResultHandler<T, TReduction> : IReductionResultHandler<T, TReduction>
            where T : struct
            where TReduction : IAtomicReduction<T>
        {
            /// <summary cref="IReductionResultHandler{T, TReduction}.Reduce(Index, Index, ArrayView{T}, TReduction)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Reduce(
                Index warpIdx,
                Index laneIdx,
                ArrayView<T> sharedMemory,
                TReduction reduction)
            {
                if (warpIdx != Index.Zero)
                    return default;

                AllReduceResultHandler<T, TReduction> resultHandler = default;
                return resultHandler.Reduce(warpIdx, laneIdx, sharedMemory, reduction);
            }
        }

        /// <summary>
        /// Implements a block-wide reduction algorithm.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="groupIdx">The current group-thread index.</param>
        /// <param name="value">The current value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>All threads in the whole group contain the reduced value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TShuffleDown, TReduction>(
            Index groupIdx,
            T value,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : IShuffleDown<T>
            where TReduction : IAtomicReduction<T> =>
            Reduce<T, TShuffleDown, TReduction, AllReduceResultHandler<T, TReduction>>(
                groupIdx,
                value,
                shuffleDown,
                reduction);

        /// <summary>
        /// Implements a block-wide reduction algorithm.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="groupIdx">The current group-thread index.</param>
        /// <param name="value">The current value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>All lanes in the first warp contain the reduced value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FirstWarpReduce<T, TShuffleDown, TReduction>(
            Index groupIdx,
            T value,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : IShuffleDown<T>
            where TReduction : IAtomicReduction<T> =>
            Reduce<T, TShuffleDown, TReduction, FirstWarpReduceResultHandler<T, TReduction>>(
                groupIdx,
                value,
                shuffleDown,
                reduction);

        /// <summary>
        /// Implements a block-wide reduction algorithm.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <typeparam name="TReductionResultHandler">The type of internal result handler.</typeparam>
        /// <param name="groupIdx">The current group-thread index.</param>
        /// <param name="value">The current value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>All lanes in the first warp contain the reduced value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Reduce<T, TShuffleDown, TReduction, TReductionResultHandler>(
            Index groupIdx,
            T value,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : IShuffleDown<T>
            where TReduction : IAtomicReduction<T>
            where TReductionResultHandler : struct, IReductionResultHandler<T, TReduction>
        {
            const int NumMemoryBanks = 4;
            var sharedMemory = SharedMemory.Allocate<T>(NumMemoryBanks);

            var warpIdx = Warp.ComputeWarpIdx(groupIdx);
            var laneIdx = Warp.LaneIdx;

            if (warpIdx == 0)
            {
                for (int bankIdx = laneIdx; bankIdx < NumMemoryBanks; bankIdx += Warp.WarpSize)
                    sharedMemory[bankIdx] = reduction.NeutralElement;
            }
            Group.Barrier();

            value = WarpExtensions.Reduce(value, shuffleDown, reduction);

            if (laneIdx == 0)
                reduction.AtomicReduce(ref sharedMemory[warpIdx % NumMemoryBanks], value);

            Group.Barrier();

            TReductionResultHandler resultHandler = default;
            return resultHandler.Reduce(warpIdx, laneIdx, sharedMemory, reduction);
        }

        #endregion
    }
}
