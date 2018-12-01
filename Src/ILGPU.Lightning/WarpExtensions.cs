// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: WarpExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.ReductionOperations;
using ILGPU.ScanOperations;
using ILGPU.ShuffleOperations;
using System.Runtime.CompilerServices;

namespace ILGPU.Lightning
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
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>The first lane (lane id = 0) will return reduced result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Reduce<T, TShuffleDown, TReduction>(
            T value,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : IShuffleDown<T>
            where TReduction : IReduction<T>
        {
            for (int laneOffset = Warp.WarpSize / 2; laneOffset > 0; laneOffset >>= 1)
            {
                var shuffled = shuffleDown.ShuffleDown(value, laneOffset);
                value = reduction.Reduce(value, shuffled);
            }
            return value;
        }

        /// <summary>
        /// Performs a warp-wide reduction.
        /// </summary>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>The first lane (lane id = 0) will return reduced result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Reduce<TReduction>(int value, TReduction reduction)
            where TReduction : IReduction<int>
        {
            return Reduce(value, new ShuffleDownInt32(), reduction);
        }

        /// <summary>
        /// Performs a warp-wide reduction.
        /// </summary>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>The first lane (lane id = 0) will return reduced result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Reduce<TReduction>(float value, TReduction reduction)
            where TReduction : IReduction<float>
        {
            return Reduce(value, new ShuffleDownFloat(), reduction);
        }

        /// <summary>
        /// Performs a warp-wide reduction.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TShuffleXor">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="shuffleXor">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>All lanes will return the reduced result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TShuffleXor, TReduction>(
            T value,
            TShuffleXor shuffleXor,
            TReduction reduction)
            where T : struct
            where TShuffleXor : IShuffleXor<T>
            where TReduction : IReduction<T>
        {
            for (int laneMask = Warp.WarpSize / 2; laneMask > 0; laneMask >>= 1)
            {
                var shuffled = shuffleXor.ShuffleXor(value, laneMask);
                value = reduction.Reduce(value, shuffled);
            }
            return value;
        }

        /// <summary>
        /// Performs a warp-wide reduction.
        /// </summary>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>All lanes will return the reduced result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AllReduce<TReduction>(int value, TReduction reduction)
            where TReduction : IReduction<int>
        {
            return AllReduce(value, new ShuffleXorInt32(), reduction);
        }

        /// <summary>
        /// Performs a warp-wide reduction.
        /// </summary>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="value">The current value.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>All lanes will return the reduced result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AllReduce<TReduction>(float value, TReduction reduction)
            where TReduction : IReduction<float>
        {
            return AllReduce(value, new ShuffleXorFloat(), reduction);
        }

        #endregion

        #region Scan

        /// <summary>
        /// Performs a warp-wide inclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TShuffleUp">The required shuffle logic.</typeparam>
        /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <param name="shuffleUp">The shuffle logic.</param>
        /// <param name="warpScan">The scan operation logic.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TShuffleUp, TScan>(T value, TShuffleUp shuffleUp, TScan warpScan)
            where T : struct
            where TShuffleUp : struct, IShuffleUp<T>
            where TScan : struct, IScanOperation<T>
        {
            var laneIdx = Warp.LaneIdx;
            for (int delta = 1; delta < Warp.WarpSize; delta <<= 1)
            {
                var otherValue = shuffleUp.ShuffleUp(value, delta);
                if (laneIdx >= delta)
                    value = warpScan.Apply(value, otherValue);
            }
            return value;
        }

        #endregion
    }
}
