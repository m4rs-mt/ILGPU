// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: CPURuntimeWarpContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a runtime context for warps.
    /// </summary>
    sealed class CPURuntimeWarpContext : DisposeBase
    {
        #region Static

        /// <summary>
        /// Represents the current context.
        /// </summary>
        [ThreadStatic]
        private static CPURuntimeWarpContext currentContext;

        /// <summary>
        /// Represents the current warp idx.
        /// </summary>
        [ThreadStatic]
        private static int currentWarpIdx;

        /// <summary>
        /// Represents the current lane idx.
        /// </summary>
        [ThreadStatic]
        private static int currentLaneIdx;

        /// <summary>
        /// Returns the current warp runtime context.
        /// </summary>
        public static CPURuntimeWarpContext Current => currentContext;

        /// <summary>
        /// Returns the current warp idx.
        /// </summary>
        public static int WarpIdx => currentWarpIdx;

        /// <summary>
        /// Returns the current lane idx.
        /// </summary>
        public static int LaneIdx => currentLaneIdx;

        /// <summary>
        /// Returns true iff the given warp size is a valid warp size.
        /// </summary>
        /// <param name="warpSize">The warp size to test.</param>
        /// <returns>True, iff the given warp size is a valid warp size.</returns>
        public static bool IsValidWarpSize(int warpSize)
        {
            return warpSize > 0 && (warpSize & (warpSize - 1)) == 0;
        }

        #endregion

        #region Instance

        private Barrier shuffleBarrier;
        private MemoryBuffer<int> shuffleBank;

        /// <summary>
        /// Constructs a new CPU-based runtime context for parallel processing.
        /// </summary>
        /// <param name="accelerator">The target CPU accelerator.</param>
        public CPURuntimeWarpContext(CPUAccelerator accelerator)
        {
            Debug.Assert(accelerator != null, "Invalid accelerator");
            WarpSize = accelerator.WarpSize;
            shuffleBarrier = new Barrier(WarpSize);
            shuffleBank = accelerator.Allocate<int>(WarpSize);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated warp size.
        /// </summary>
        public int WarpSize { get; }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private VariableView<T> GetLaneView<T>(int laneIdx)
            where T : struct
        {
            return shuffleBank.View.GetVariableView(laneIdx).Cast<T>();
        }

        /// <summary>
        /// Enters a shuffle operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="width">The width of the shuffle operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnterShuffle<T>(T variable, int width)
            where T : struct
        {
            Debug.Assert(IsValidWarpSize(width) && width <= WarpSize, "Invalid warp size");
            GetLaneView<T>(LaneIdx).Store(variable);
            Thread.MemoryBarrier();
            shuffleBarrier.SignalAndWait();
        }

        /// <summary>
        /// Exits a shuffle operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="laneIdx">The absolute source lane index to load from.</param>
        /// <returns>The loaded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ExitShuffle<T>(int laneIdx)
            where T : struct
        {
            var value = GetLaneView<T>(laneIdx).Load();
            return ExitShuffle(value);
        }

        /// <summary>
        /// Exits a shuffle operation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <returns>The shuffle variable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ExitShuffle<T>(T variable)
            where T : struct
        {
            shuffleBarrier.SignalAndWait();
            return variable;
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the specified source lane.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="sourceLane">The source lane.</param>
        /// <param name="width">The width of the shuffle operation.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        /// <remarks>Note that all threads in a warp should participate in the shuffle operation.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Shuffle<T>(T variable, int sourceLane, int width)
            where T : struct
        {
            EnterShuffle(variable, width);
            return ExitShuffle<T>(sourceLane % width);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane + delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to add to the current lane.</param>
        /// <param name="width">The width of the shuffle operation.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ShuffleDown<T>(T variable, int delta, int width)
            where T : struct
        {
            EnterShuffle(variable, width);
            var wrappedLane = (LaneIdx % width) + delta;
            if (wrappedLane < 0 || wrappedLane >= width)
                return ExitShuffle(variable);
            return ExitShuffle<T>(LaneIdx + delta);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane - delta.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="delta">The delta to subtract to the current lane.</param>
        /// <param name="width">The width of the shuffle operation.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ShuffleUp<T>(T variable, int delta, int width)
            where T : struct
        {
            EnterShuffle(variable, width);
            var wrappedLane = (LaneIdx % width) - delta;
            if (wrappedLane < 0 || wrappedLane >= width)
                return ExitShuffle(variable);
            return ExitShuffle<T>(LaneIdx - delta);
        }

        /// <summary>
        /// Performs a shuffle operation. It returns the value of the variable
        /// in the context of the lane with the id current lane xor mask.
        /// The width of the shuffle operation is the warp size.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="mask">The mask to xor to the current lane.</param>
        /// <param name="width">The width of the shuffle operation.</param>
        /// <returns>The value of the variable in the scope of the desired lane.</returns>
        public T ShuffleXor<T>(T variable, int mask, int width)
            where T : struct
        {
            Debug.Assert(width == WarpSize, "Xor-shuffle is currently only supported in cases of width=WarpSize");
            EnterShuffle(variable, width);
            var sourceLane = LaneIdx ^ mask;
            return ExitShuffle<T>(sourceLane);
        }

        /// <summary>
        /// Initializes the wrap context.
        /// </summary>
        /// <param name="runtimeGroupThreadIdx">The current relative group-thread index.</param>
        /// <param name="runtimeThreadOffset">The thread offset within the current group (WarpId * WarpSize + WarpThreadIdx).</param>
        public void Initialize(
            int runtimeGroupThreadIdx,
            out int runtimeThreadOffset)
        {
            Debug.Assert(runtimeGroupThreadIdx >= 0);
            int warpIdx, laneIdx;
            currentWarpIdx = warpIdx = runtimeGroupThreadIdx / WarpSize;
            currentLaneIdx = laneIdx = runtimeGroupThreadIdx % WarpSize;
            runtimeThreadOffset = warpIdx * WarpSize + laneIdx;
        }

        /// <summary>
        /// Makes the current context the active one for this thread.
        /// </summary>
        internal void MakeCurrent()
        {
            currentContext = this;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "shuffleBarrier", Justification = "Dispose method will be invoked by a helper method")]
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "shuffleBank", Justification = "Dispose method will be invoked by a helper method")]
        protected override void Dispose(bool disposing)
        {
            if (shuffleBarrier == null)
                return;

            Dispose(ref shuffleBarrier);
            Dispose(ref shuffleBank);
        }

        #endregion
    }
}
