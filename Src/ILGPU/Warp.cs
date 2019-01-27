// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Warp.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

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
            get { return 1; }
        }

        /// <summary>
        /// Returns the current lane index [0, WarpSize - 1].
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        public static int LaneIdx
        {
            [WarpIntrinsic(WarpIntrinsicKind.LaneIdx)]
            get { return 0; }
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeWarpIdx(Index groupThreadIdx) => groupThreadIdx / WarpSize;

        /// <summary>
        /// Computes the current thread within a warp in the range [0, WarpSize - 1].
        /// </summary>
        /// <param name="groupThreadIdx">The current thread index within the current group.</param>
        /// <returns>The current warp thread index in the range [0, WarpSize - 1].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeWarpThreadIdx(Index groupThreadIdx) => groupThreadIdx % WarpSize;

        #endregion

        #region Barriers

        /// <summary>
        /// Executes a thread barrier in the scope of a warp.
        /// </summary>
        [WarpIntrinsic(WarpIntrinsicKind.Barrier)]
        public static void Barrier()
        {
            // This may need to be extended in the future in order
            // to support more sophisticated warp setups on the CPU.
            Thread.MemoryBarrier();
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
        [WarpIntrinsic(WarpIntrinsicKind.Shuffle)]
        public static int Shuffle(int variable, int sourceLane) => variable;

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffle)]
        public static int Shuffle(int variable, int sourceLane, int width)
        {
            Debug.Assert(width <= WarpSize, "Not supported shuffle width");
            return variable;
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
        [WarpIntrinsic(WarpIntrinsicKind.Shuffle)]
        public static float Shuffle(float variable, int sourceLane) => variable;

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffle)]
        public static float Shuffle(float variable, int sourceLane, int width)
        {
            Debug.Assert(width <= WarpSize, "Not supported shuffle width");
            return variable;
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
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleDown)]
        public static int ShuffleDown(int variable, int delta) => variable;

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleDown)]
        public static int ShuffleDown(int variable, int delta, int width)
        {
            Debug.Assert(width <= WarpSize, "Not supported shuffle width");
            return variable;
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
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleDown)]
        public static float ShuffleDown(float variable, int delta) => variable;

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleDown)]
        public static float ShuffleDown(float variable, int delta, int width)
        {
            Debug.Assert(width <= WarpSize, "Not supported shuffle width");
            return variable;
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
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleUp)]
        public static int ShuffleUp(int variable, int delta) => variable;

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleUp)]
        public static int ShuffleUp(int variable, int delta, int width)
        {
            Debug.Assert(width <= WarpSize, "Not supported shuffle width");
            return variable;
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
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleUp)]
        public static float ShuffleUp(float variable, int delta) => variable;

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleUp)]
        public static float ShuffleUp(float variable, int delta, int width)
        {
            Debug.Assert(width <= WarpSize, "Not supported shuffle width");
            return variable;
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
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleXor)]
        public static int ShuffleXor(int variable, int mask) => variable;

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleXor)]
        public static int ShuffleXor(int variable, int mask, int width)
        {
            Debug.Assert(width <= WarpSize, "Not supported shuffle width");
            return variable;
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
        [WarpIntrinsic(WarpIntrinsicKind.ShuffleXor)]
        public static float ShuffleXor(float variable, int mask) => variable;

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// </summary>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <param name="width">The width of the shuffle operation. Width must be a power of 2.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [WarpIntrinsic(WarpIntrinsicKind.SubShuffleXor)]
        public static float ShuffleXor(float variable, int mask, int width)
        {
            Debug.Assert(width <= WarpSize, "Not supported shuffle width");
            return variable;
        }

        #endregion
    }
}
