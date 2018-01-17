// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Warp.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler.Intrinsic;
using ILGPU.ReductionOperations;
using ILGPU.Runtime.CPU;
using ILGPU.ShuffleOperations;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public static int WarpSize
        {
            [WarpIntrinsic(WarpIntrinsicKind.WarpSize)]
            get { return CPURuntimeWarpContext.Current.WarpSize; }
        }


        /// <summary>
        /// Returns the current lane index [0, WarpSize - 1].
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public static int LaneIdx
        {
            [WarpIntrinsic(WarpIntrinsicKind.LaneIdx)]
            get { return CPURuntimeWarpContext.LaneIdx; }
        }

        /// <summary>
        /// Returns true iff the current lane is the first lane.
        /// </summary>
        public static bool IsFirstLane => LaneIdx == 0;

        /// <summary>
        /// Computes the current warp index in the range [0, NumUsedWarps - 1].
        /// </summary>
        /// <param name="groupThreadIdx">The current thread index within the current group.</param>
        /// <returns>The current warp index in the range [0, NumUsedWarps - 1].</returns>
        public static int ComputeWarpIdx(Index groupThreadIdx)
        {
            return groupThreadIdx / WarpSize;
        }

        #endregion

        #region Shuffle

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        public static int Shuffle(int variable, int sourceLane)
        {
            return Shuffle(variable, sourceLane, WarpSize);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleI32)]
        public static int Shuffle(int variable, int sourceLane, int width)
        {
            return CPURuntimeWarpContext.Current.Shuffle(variable, sourceLane, width);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        public static float Shuffle(float variable, int sourceLane)
        {
            return Shuffle(variable, sourceLane, WarpSize);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleF32)]
        public static float Shuffle(float variable, int sourceLane, int width)
        {
            return CPURuntimeWarpContext.Current.Shuffle(variable, sourceLane, width);
        }

        #endregion

        #region Shuffle Down

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        public static int ShuffleDown(int variable, int delta)
        {
            return ShuffleDown(variable, delta, WarpSize);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleDownI32)]
        public static int ShuffleDown(int variable, int delta, int width)
        {
            return CPURuntimeWarpContext.Current.ShuffleDown(variable, delta, width);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        public static float ShuffleDown(float variable, int delta)
        {
            return ShuffleDown(variable, delta, WarpSize);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleDownF32)]
        public static float ShuffleDown(float variable, int delta, int width)
        {
            return CPURuntimeWarpContext.Current.ShuffleDown(variable, delta, width);
        }

        #endregion

        #region Shuffle Up

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        public static int ShuffleUp(int variable, int delta)
        {
            return ShuffleUp(variable, delta, WarpSize);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleUpI32)]
        public static int ShuffleUp(int variable, int delta, int width)
        {
            return CPURuntimeWarpContext.Current.ShuffleUp(variable, delta, width);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        public static float ShuffleUp(float variable, int delta)
        {
            return ShuffleUp(variable, delta, WarpSize);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleUpF32)]
        public static float ShuffleUp(float variable, int delta, int width)
        {
            return CPURuntimeWarpContext.Current.ShuffleUp(variable, delta, width);
        }

        #endregion

        #region Shuffle Xor

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        public static int ShuffleXor(int variable, int mask)
        {
            return ShuffleXor(variable, mask, WarpSize);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleXorI32)]
        public static int ShuffleXor(int variable, int mask, int width)
        {
            return CPURuntimeWarpContext.Current.ShuffleXor(variable, mask, width);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        public static float ShuffleXor(float variable, int mask)
        {
            return ShuffleXor(variable, mask, WarpSize);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleXorF32)]
        public static float ShuffleXor(float variable, int mask, int width)
        {
            return CPURuntimeWarpContext.Current.ShuffleXor(variable, mask, width);
        }

        #endregion

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
            for (int laneOffset = WarpSize / 2; laneOffset > 0; laneOffset >>= 1)
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
            for (int laneMask = WarpSize / 2; laneMask > 0; laneMask >>= 1)
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
    }
}
