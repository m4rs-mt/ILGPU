// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Warp.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.Runtime.CPU;
using ILGPU.Util;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static ILGPU.Runtime.CPU.CPURuntimeWarpContext;

namespace ILGPU
{
    /// <summary>
    /// Contains warp-wide functions.
    /// </summary>
    public static class Warp
    {
        #region General properties

        /// <summary>
        /// Returns the warp size.
        /// </summary>
        public static int WarpSize
        {
            [WarpIntrinsic(WarpIntrinsicKind.WarpSize)]
            get => Current.WarpSize;
        }

        /// <summary>
        /// Returns the current lane index [0, WarpSize - 1].
        /// </summary>
        public static int LaneIdx
        {
            [WarpIntrinsic(WarpIntrinsicKind.LaneIdx)]
            get => CPURuntimeThreadContext.Current.LaneIndex;
        }

        /// <summary>
        /// Returns true if the current lane is the first lane.
        /// </summary>
        public static bool IsFirstLane => LaneIdx == 0;

        /// <summary>
        /// Returns true if the current lane is the last lane.
        /// </summary>
        public static bool IsLastLane => LaneIdx == WarpSize - 1;

        /// <summary>
        /// Returns the current warp index in the range [0, NumUsedWarps - 1].
        /// </summary>
        /// <returns>The current warp index in the range [0, NumUsedWarps - 1].</returns>
        public static int WarpIdx => ComputeWarpIdx(Group.IdxX);

        /// <summary>
        /// Computes the current warp index in the range [0, NumUsedWarps - 1].
        /// </summary>
        /// <param name="groupThreadIdx">
        /// The current thread index within the current group.
        /// </param>
        /// <returns>
        /// The current warp index in the range [0, NumUsedWarps - 1].
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeWarpIdx(Index1D groupThreadIdx) =>
            groupThreadIdx / WarpSize;

        /// <summary>
        /// Computes the current thread within a warp in the range [0, WarpSize - 1].
        /// </summary>
        /// <param name="groupThreadIdx">
        /// The current thread index within the current group.
        /// </param>
        /// <returns>
        /// The current warp thread index in the range [0, WarpSize - 1].
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeWarpThreadIdx(Index1D groupThreadIdx) =>
            groupThreadIdx % WarpSize;

        #endregion

        #region Barriers

        /// <summary>
        /// Executes a thread barrier in the scope of a warp.
        /// </summary>
        [WarpIntrinsic(WarpIntrinsicKind.Barrier)]
        public static void Barrier() => Current.Barrier();

        #endregion

        #region Util

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ShuffleConfig GetShuffleConfig(int sourceLane) =>
            new ShuffleConfig(
                LaneIdx,
                sourceLane,
                0,
                WarpSize);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ShuffleConfig GetSubShuffleConfig(
            int sourceLane,
            int width)
        {
            Trace.Assert(
                width > 0 && width <= WarpSize && Utilities.IsPowerOf2(width),
                "Invalid warp shuffle width");
            int lane = sourceLane % width;
            int offset = sourceLane / width * width;
            return new ShuffleConfig(
                LaneIdx,
                lane,
                offset,
                width);
        }

        #endregion

        #region Shuffle

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <typeparam name="T">The value type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        /// <remarks>
        /// Note that all threads in a warp should participate in the shuffle operation.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.Shuffle)]
        public static T Shuffle<T>(T variable, int sourceLane)
            where T : unmanaged =>
            ShuffleInternal(variable, sourceLane);

        /// <summary>
        /// Internal wrapper that implements shuffle operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T ShuffleInternal<T>(T variable, int sourceLane)
            where T : unmanaged =>
            Current.Shuffle(variable, GetShuffleConfig(sourceLane));

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// </summary>
        /// <typeparam name="T">The value type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <param name="width">
        /// The width of the shuffle operation. Width must be a power of 2.
        /// </param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        /// <remarks>
        /// Note that all threads in a warp should participate in the shuffle operation.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffle)]
        public static T Shuffle<T>(T variable, int sourceLane, int width)
            where T : unmanaged =>
            SubShuffleInternal(variable, sourceLane, width);

        /// <summary>
        /// Internal wrapper that implements shuffle operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T SubShuffleInternal<T>(T variable, int sourceLane, int width)
            where T : unmanaged =>
            Current.Shuffle(variable, GetSubShuffleConfig(sourceLane, width));

        #endregion

        #region Shuffle Down

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <typeparam name="T">The value type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        /// <remarks>
        /// Note that all threads in a warp should participate in the shuffle operation.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleDown)]
        public static T ShuffleDown<T>(T variable, int delta)
            where T : unmanaged =>
            ShuffleDownInternal(variable, delta);

        /// <summary>
        /// Internal wrapper that implements shuffle down operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T ShuffleDownInternal<T>(T variable, int delta)
            where T : unmanaged =>
            Shuffle(variable, LaneIdx + delta);

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// </summary>
        /// <typeparam name="T">The value type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <param name="width">
        /// The width of the shuffle operation. Width must be a power of 2.
        /// </param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        /// <remarks>
        /// Note that all threads in a warp should participate in the shuffle operation.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleDown)]
        public static T ShuffleDown<T>(T variable, int delta, int width)
            where T : unmanaged =>
            SubShuffleDownInternal(variable, delta, width);

        /// <summary>
        /// Internal wrapper that implements shuffle down operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T SubShuffleDownInternal<T>(T variable, int delta, int width)
            where T : unmanaged
        {
            var config = GetSubShuffleConfig(LaneIdx, width);
            return Current.Shuffle(
                variable,
                config.AdjustSourceLane(config.SourceLane + delta));
        }

        #endregion

        #region Shuffle Up

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <typeparam name="T">The value type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        /// <remarks>
        /// Note that all threads in a warp should participate in the shuffle operation.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleUp)]
        public static T ShuffleUp<T>(T variable, int delta)
            where T : unmanaged =>
            ShuffleUpInternal(variable, delta);

        /// <summary>
        /// Internal wrapper that implements shuffle up operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T ShuffleUpInternal<T>(T variable, int delta)
            where T : unmanaged =>
            Shuffle(variable, LaneIdx - delta);

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// </summary>
        /// <typeparam name="T">The value type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <param name="width">
        /// The width of the shuffle operation. Width must be a power of 2.
        /// </param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        /// <remarks>
        /// Note that all threads in a warp should participate in the shuffle operation.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleUp)]
        public static T ShuffleUp<T>(T variable, int delta, int width)
            where T : unmanaged =>
            SubShuffleUpInternal(variable, delta, width);

        /// <summary>
        /// Internal wrapper that implements shuffle up operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T SubShuffleUpInternal<T>(T variable, int delta, int width)
            where T : unmanaged
        {
            var config = GetSubShuffleConfig(LaneIdx, width);
            return Current.Shuffle(
                variable,
                config.AdjustSourceLane(config.SourceLane - delta));
        }

        #endregion

        #region Shuffle Xor

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <typeparam name="T">The type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        /// <remarks>
        /// Note that all threads in a warp should participate in the shuffle operation.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleXor)]
        public static T ShuffleXor<T>(T variable, int mask)
            where T : unmanaged =>
            ShuffleXorInternal(variable, mask);

        /// <summary>
        /// Internal wrapper that implements shuffle xor operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T ShuffleXorInternal<T>(T variable, int mask)
            where T : unmanaged =>
            Shuffle(variable, LaneIdx ^ mask);

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// </summary>
        /// <typeparam name="T">The type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <param name="width">
        /// The width of the shuffle operation. Width must be a power of 2.
        /// </param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        /// <remarks>
        /// Note that all threads in a warp should participate in the shuffle operation.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleXor)]
        public static T ShuffleXor<T>(T variable, int mask, int width)
            where T : unmanaged =>
            SubShuffleXorInternal(variable, mask, width);

        /// <summary>
        /// Internal wrapper that implements shuffle xor operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T SubShuffleXorInternal<T>(T variable, int mask, int width)
            where T : unmanaged
        {
            var config = GetSubShuffleConfig(LaneIdx, width);
            return Current.Shuffle(
                variable,
                config.AdjustSourceLane(config.SourceLane ^ mask));
        }

        #endregion

        #region Broadcast

        /// <summary>
        /// Performs a broadcast operation that broadcasts the given value
        /// from the specified thread to all other threads in the warp.
        /// </summary>
        /// <typeparam name="T">The type to broadcast.</typeparam>
        /// <param name="value">The value to broadcast.</param>
        /// <param name="sourceLane">The source thread index within the warp.</param>
        /// <remarks>
        /// Note that the source lane must be the same for all threads in the warp.
        /// </remarks>
        [WarpIntrinsic(WarpIntrinsicKind.Broadcast)]
        public static T Broadcast<T>(T value, int sourceLane)
            where T : unmanaged =>
            Current.Broadcast(value, sourceLane);

        #endregion
    }
}
