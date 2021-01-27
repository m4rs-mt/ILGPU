// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPURuntimeWarpContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
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
    sealed class CPURuntimeWarpContext : CPURuntimeContext
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
            public GetShuffleMemory(MemoryBufferCache shuffleBuffer, int warpSize)
            {
                ShuffleBuffer = shuffleBuffer;
                WarpSize = warpSize;
            }

            /// <summary>
            /// Returns the parent context.
            /// </summary>
            public MemoryBufferCache ShuffleBuffer { get; }

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
            public readonly ArrayView<T> Result => ShuffleBuffer.Cache.View.Cast<T>();
        }

        #endregion

        #region Instance

        /// <summary>
        /// A temporary location for shuffle values.
        /// </summary>
        private readonly MemoryBufferCache shuffleBuffer;

        /// <summary>
        /// Constructs a new CPU-based runtime context for parallel processing.
        /// </summary>
        /// <param name="accelerator">The target CPU accelerator.</param>
        /// <param name="numThreadsPerWarp">The number of threads per warp.</param>
        public CPURuntimeWarpContext(CPUAccelerator accelerator, int numThreadsPerWarp)
            : base(accelerator)
        {
            WarpSize = numThreadsPerWarp;

            shuffleBuffer = new MemoryBufferCache(accelerator);
            shuffleBuffer.Allocate<int>(2 * sizeof(int) * numThreadsPerWarp);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of threads per warp.
        /// </summary>
        public int WarpSize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Executes a thread barrier.
        /// </summary>
        public override void Barrier() => WaitForAllThreads();

        /// <summary>
        /// Performs a shuffle operation.
        /// </summary>
        /// <typeparam name="T">The value type to shuffle.</typeparam>
        /// <param name="variable">The source variable to shuffle.</param>
        /// <param name="config">The current shuffle configuration.</param>
        /// <returns>
        /// The value of the variable in the scope of the desired lane.
        /// </returns>
        public T Shuffle<T>(T variable, in ShuffleConfig config)
            where T : unmanaged
        {
            config.Validate(WarpSize);

            // Allocate a compatible view
            var view = PerformLocked<
                GetShuffleMemory<T>,
                ArrayView<T>>(
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
        /// Initializes this context.
        /// </summary>
        /// <param name="numLanes">The number of active lanes.</param>
        internal new void Initialize(int numLanes) => base.Initialize(numLanes);

        /// <summary>
        /// Called when a CPU kernel has finished, reducing the number of participants in
        /// future calls to Barrier-related methods.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FinishThreadProcessing() => RemoveBarrierParticipant();

        /// <summary>
        /// Makes the current context the active one for this thread.
        /// </summary>
        internal void MakeCurrent() => currentContext = this;

        #endregion
    }
}
