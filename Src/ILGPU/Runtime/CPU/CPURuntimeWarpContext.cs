// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPURuntimeWarpContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a runtime context for a single warp.
    /// </summary>
    sealed class CPURuntimeWarpContext : CPURuntimeContext, CPURuntimeContext.IParent
    {
        #region Thread Static

        /// <summary>
        /// Represents the current context.
        /// </summary>
        [ThreadStatic]
        private static CPURuntimeWarpContext currentContext;

        /// <summary>
        /// Returns the current warp runtime context.
        /// </summary>
        public static CPURuntimeWarpContext Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Trace.Assert(
                    currentContext != null,
                    ErrorMessages.InvalidKernelOperation);
                return currentContext;
            }
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a single configuration on how to perform a shuffle operation.
        /// </summary>
        public readonly struct ShuffleConfig
        {
            /// <summary>
            /// Constructs a new shuffle configuration.
            /// </summary>
            public ShuffleConfig(
                int currentLane,
                int sourceLane,
                int offset,
                int width)
            {
                CurrentLane = currentLane;
                SourceLane = sourceLane;
                Offset = offset;
                Width = width;
            }

            /// <summary>
            /// Returns the current contributing lane index.
            /// </summary>
            public int CurrentLane { get; }

            /// <summary>
            /// Returns the relative source lane to shuffle from.
            /// </summary>
            public int SourceLane { get; }

            /// <summary>
            /// Returns the absolute offset to convert the relative source lane value
            /// into an absolute lane index to shuffle from.
            /// </summary>
            public int Offset { get; }

            /// <summary>
            /// Returns the logical warp size to use.
            /// </summary>
            public int Width { get; }

            /// <summary>
            /// Returns true if the current relative source lane is in bounds of the
            /// currently specified sub-warp range.
            /// </summary>
            public readonly bool IsSourceLaneInBounds =>
                SourceLane >= 0 && SourceLane < Width;

            /// <summary>
            /// Returns the absolute source lane index.
            /// </summary>
            public readonly int AbsoluteSourceLane => SourceLane + Offset;

            /// <summary>
            /// Validates the current configuration using the given warp size.
            /// </summary>
            /// <param name="warpSize">The current warp size.</param>
            internal readonly void Validate(int warpSize)
            {
                Trace.Assert(
                    CurrentLane >= 0 && CurrentLane < warpSize,
                    "Invalid current lane to shuffle from");
                Trace.Assert(
                    !IsSourceLaneInBounds ||
                    AbsoluteSourceLane >= 0 && AbsoluteSourceLane < warpSize,
                    "Invalid source lane to shuffle from");
            }

            /// <summary>
            /// Adjusts the internally stored source lane index.
            /// </summary>
            /// <param name="sourceLane">The new source lane index to use.</param>
            /// <returns>The updated configuration.</returns>
            public readonly ShuffleConfig AdjustSourceLane(int sourceLane) =>
                new ShuffleConfig(
                    CurrentLane,
                    sourceLane,
                    Offset,
                    Width);
        }

        /// <summary>
        /// Represents an operation that allocates and managed shuffle memory.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        private readonly struct GetShuffleMemory<T> : ILockedOperation<ArrayView<T>>
            where T : unmanaged
        {
            /// <summary>
            /// Constructs a new allocation operation.
            /// </summary>
            public GetShuffleMemory(CPUMemoryBufferCache shuffleBuffer, int warpSize)
            {
                ShuffleBuffer = shuffleBuffer;
                WarpSize = warpSize;
            }

            /// <summary>
            /// Returns the parent context.
            /// </summary>
            public CPUMemoryBufferCache ShuffleBuffer { get; }

            /// <summary>
            /// Returns the warp size.
            /// </summary>
            public int WarpSize { get; }

            /// <summary>
            /// Allocates the required amount of shuffle memory.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void ApplySyncInMainThread()
            {
                // Allocate the requested amount of elements
                long sizeInBytes = WarpSize * Interop.SizeOf<T>();
                ShuffleBuffer.Allocate<T>(sizeInBytes);
            }

            /// <summary>
            /// Returns a view to the (potentially) adjusted shuffle cache.
            /// </summary>
            public readonly ArrayView<T> Result => ShuffleBuffer.Cache.Cast<T>();
        }

        #endregion

        #region Instance

        /// <summary>
        /// A temporary location for shuffle values.
        /// </summary>
        private readonly CPUMemoryBufferCache shuffleBuffer;

        /// <summary>
        /// Constructs a new CPU-based runtime context for parallel processing.
        /// </summary>
        /// <param name="multiprocessor">The target CPU multiprocessor.</param>
        /// <param name="numThreadsPerWarp">The number of threads per warp.</param>
        public CPURuntimeWarpContext(
            CPUMultiprocessor multiprocessor,
            int numThreadsPerWarp)
            : base(multiprocessor)
        {
            WarpSize = numThreadsPerWarp;

            shuffleBuffer = new CPUMemoryBufferCache(Multiprocessor.Accelerator);
            shuffleBuffer.Allocate<int>(2 * sizeof(int) * numThreadsPerWarp);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of threads per warp (statically known).
        /// </summary>
        public int WarpSize { get; }

        /// <summary>
        /// Returns the number of threads per warp in the current runtime context.
        /// </summary>
        public int CurrentWarpSize { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Executes a thread barrier.
        /// </summary>
        /// <returns>The number of participating threads.</returns>
        public int Barrier() => Multiprocessor.WarpBarrier();

        /// <summary>
        /// Performs a shuffle operation.
        /// </summary>
        /// <typeparam name="T">The value type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="config">The current shuffle configuration.</param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Shuffle<T>(T variable, in ShuffleConfig config)
            where T : unmanaged
        {
            config.Validate(WarpSize);

            // Allocate a compatible view
            var view = PerformLocked<
                CPURuntimeWarpContext,
                GetShuffleMemory<T>,
                ArrayView<T>>(
                this,
                new GetShuffleMemory<T>(shuffleBuffer, WarpSize));

            // Fill the shared view with data of each lane
            view[config.CurrentLane] = variable;
            Barrier();

            // Get the resulting values
            var result = config.IsSourceLaneInBounds
                ? view[config.AbsoluteSourceLane]
                : variable;
            Barrier();

            return result;
        }

        /// <summary>
        /// Executes a broadcast operation.
        /// </summary>
        /// <typeparam name="T">The element type to broadcast.</typeparam>
        /// <param name="value">The desired group index.</param>
        /// <param name="laneIndex">The source thread index within the warp.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Broadcast<T>(T value, int laneIndex)
            where T : unmanaged =>
            Broadcast(
                this,
                value,
                CPURuntimeThreadContext.Current.LaneIndex,
                laneIndex);

        /// <summary>
        /// Initializes this context.
        /// </summary>
        /// <param name="currentWarpSize">The current warp size.</param>
        public void Initialize(int currentWarpSize)
        {
            CurrentWarpSize = currentWarpSize;
            Initialize();
        }

        /// <summary>
        /// Makes the current context the active one for this thread.
        /// </summary>
        internal void MakeCurrent() => currentContext = this;

        #endregion
    }
}
