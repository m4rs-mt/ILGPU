// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CPURuntimeGroupContext.cs
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
    /// Represents a runtime context for thread groups.
    /// </summary>
    sealed class CPURuntimeGroupContext : DisposeBase
    {
        #region Thread Static

        /// <summary>
        /// Represents the current context.
        /// </summary>
        [ThreadStatic]
        private static CPURuntimeGroupContext currentContext;

        /// <summary>
        /// A counter for the computation of interlocked group counters.
        /// </summary>
        private int groupCounter;

        /// <summary>
        /// Returns the current group runtime context.
        /// </summary>
        public static CPURuntimeGroupContext Current => currentContext;

        #endregion

        #region Instance

        private Barrier groupBarrier;
        private MemoryBufferCache sharedMemoryBuffer;

        /// <summary>
        /// Constructs a new CPU-based runtime context for parallel processing.
        /// </summary>
        /// <param name="accelerator">The target CPU accelerator.</param>
        public CPURuntimeGroupContext(CPUAccelerator accelerator)
        {
            Debug.Assert(accelerator != null, "Invalid accelerator");
            groupBarrier = new Barrier(0);
            sharedMemoryBuffer = new MemoryBufferCache(accelerator);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current group dimension.
        /// </summary>
        public Index3 GroupDim { get; private set; }

        /// <summary>
        /// Returns the current total group size in number of threads.
        /// </summary>
        public int GroupSize => GroupDim.Size;

        /// <summary>
        /// Returns the associated shared memory.
        /// </summary>
        public ArrayView<byte> SharedMemory { get; private set; }

        /// <summary>
        /// Returns the associated warp size.
        /// </summary>
        public int WarpSize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Executes a thread barrier.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Barrier()
        {
            Thread.MemoryBarrier();
            groupBarrier.SignalAndWait();
        }

        /// <summary>
        /// Executes a thread barrier and returns the number of threads for which
        /// the predicate evaluated to true.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>The number of threads for which the predicate evaluated to true.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BarrierPopCount(bool predicate)
        {
            Interlocked.Exchange(ref groupCounter, 0);
            Barrier();
            if (predicate)
                Interlocked.Increment(ref groupCounter);
            groupBarrier.SignalAndWait();
            var result = Interlocked.CompareExchange(ref groupCounter, 0, 0);
            groupBarrier.SignalAndWait();
            return result;
        }

        /// <summary>
        /// Executes a thread barrier and returns true iff all threads in a block
        /// fullfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, iff all threads in a block fullfills the predicate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BarrierAnd(bool predicate)
        {
            return BarrierPopCount(predicate) == groupBarrier.ParticipantCount;
        }

        /// <summary>
        /// Executes a thread barrier and returns true iff any thread in a block
        /// fullfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, iff any thread in a block fullfills the predicate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BarrierOr(bool predicate)
        {
            return BarrierPopCount(predicate) > 0;
        }

        /// <summary>
        /// Initializes this context.
        /// </summary>
        /// <param name="groupDim">The group dimension.</param>
        /// <param name="sharedMemSize">The required shared-memory size in bytes used by this group.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(Index3 groupDim, int sharedMemSize)
        {
            GroupDim = groupDim;

            var currentBarrierCount = groupBarrier.ParticipantCount;
            if (currentBarrierCount > GroupSize)
                groupBarrier.RemoveParticipants(currentBarrierCount - GroupSize);
            else if (currentBarrierCount < GroupSize)
                groupBarrier.AddParticipants(GroupSize - currentBarrierCount);

            if (sharedMemSize > 0)
                SharedMemory = sharedMemoryBuffer.Allocate<byte>(sharedMemSize);
            else
                SharedMemory = new ArrayView<byte>();
        }

        /// <summary>
        /// Makes the current context the active one for this thread.
        /// </summary>
        /// <param name="sharedMemory">Outputs the current shared-memory view.</param>
        /// <param name="groupSynchronizationBarrier">Outputs the current group barrier.</param>
        /// <returns>The associated shared memory.</returns>
        internal void MakeCurrent(
            out ArrayView<byte> sharedMemory,
            out Barrier groupSynchronizationBarrier)
        {
            currentContext = this;
            sharedMemory = SharedMemory;
            groupSynchronizationBarrier = groupBarrier;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "groupBarrier", Justification = "Dispose method will be invoked by a helper method")]
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "sharedMemoryBuffer", Justification = "Dispose method will be invoked by a helper method")]
        protected override void Dispose(bool disposing)
        {
            if (groupBarrier == null)
                return;

            Dispose(ref groupBarrier);
            Dispose(ref sharedMemoryBuffer);
        }

        #endregion
    }
}
