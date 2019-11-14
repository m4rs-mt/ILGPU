// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CPURuntimeGroupContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a runtime context for thread groups.
    /// </summary>
    public sealed class CPURuntimeGroupContext : DisposeBase
    {
        #region Constants

        /// <summary>
        /// The maximum shared-memory size for a single group.
        /// </summary>
        internal const int SharedMemorySize = int.MaxValue;

        /// <summary>
        /// The chunk size of shared-memory to allocate in a row.
        /// </summary>
        internal const int SharedMemoryChunkSize = 8 * 1024 * 1024;

        /// <summary>
        /// The maximum broadcast buffer size for a single object.
        /// </summary>
        private const int BroadcastBufferSize = 8 * 1024;

        #endregion

        #region Thread Static

        /// <summary>
        /// Represents the current context.
        /// </summary>
        [ThreadStatic]
        private static CPURuntimeGroupContext currentContext;

        /// <summary>
        /// Returns the current group runtime context.
        /// </summary>
        public static CPURuntimeGroupContext Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(currentContext != null, ErrorMessages.InvalidKernelOperation);
                return currentContext;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// A counter for the computation of interlocked group counters.
        /// </summary>
        private volatile int groupCounter;

        /// <summary>
        /// The general group barrier.
        /// </summary>
        private readonly Barrier groupBarrier;

        /// <summary>
        /// A temporary location for broadcast values.
        /// </summary>
        private readonly MemoryBuffer<byte> broadcastBuffer;

        /// <summary>
        /// The current shared memory offset for allocation.
        /// </summary>
        private volatile int sharedMemoryOffset = 0;

        /// <summary>
        /// The global shared memory lock variable.
        /// </summary>
        private volatile int sharedMemoryLock = 0;

        /// <summary>
        /// The current shared-memory view.
        /// </summary>
        private ArrayView<byte> currentSharedMemoryView;

        /// <summary>
        /// The actual shared-memory buffer.
        /// </summary>
        private readonly MemoryBufferCache sharedMemoryBuffer;

        // TODO: remove advancedSharedMemoryBuffer by adjusting
        // the IL-code in debug builds

        /// <summary>
        /// A temporary cache for additional shared memory requirements.
        /// </summary>
        /// <remarks>
        /// Note that this buffer is only required for debug CPU builds. In
        /// these cases, we cannot move nested <see cref="SharedMemory.Allocate{T}(int)"/>
        /// instructions out of nested loops to provide the best debugging experience.
        /// </remarks>
        private readonly List<MemoryBuffer<byte>> advancedSharedMemoryBuffer =
            new List<MemoryBuffer<byte>>();

        /// <summary>
        /// Represents the next advanced shared-memory buffer index to use.
        /// </summary>
        private volatile int advancedSharedMemoryBufferIndex = -1;

        /// <summary>
        /// Constructs a new CPU-based runtime context for parallel processing.
        /// </summary>
        /// <param name="accelerator">The target CPU accelerator.</param>
        public CPURuntimeGroupContext(CPUAccelerator accelerator)
        {
            Debug.Assert(accelerator != null, "Invalid accelerator");
            Accelerator = accelerator;
            groupBarrier = new Barrier(0);
            sharedMemoryBuffer = new MemoryBufferCache(accelerator);
            broadcastBuffer = accelerator.Allocate<byte>(BroadcastBufferSize);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public CPUAccelerator Accelerator { get; }

        /// <summary>
        /// Returns the associated shared memory.
        /// </summary>
        public ArrayView<byte> SharedMemory { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Performs an internal shared-memory allocation.
        /// </summary>
        /// <param name="extent">The number of elements to allocate.</param>
        private void AllocateSharedMemoryInternal<T>(int extent)
            where T : struct
        {
            int sizeInBytes = extent * Interop.SizeOf<T>();
            if (advancedSharedMemoryBufferIndex < 0)
            {
                // We can allocate the required memory
                if (sharedMemoryOffset + sizeInBytes <= SharedMemory.Length)
                {
                    currentSharedMemoryView = SharedMemory.GetSubView(sharedMemoryOffset, sizeInBytes);
                    sharedMemoryOffset += sizeInBytes;
                    return;
                }

                // We have to perform an advanced buffer allocation
                // -> ...
            }
            else
            {
                // Use the advanced buffer
                var buffer = advancedSharedMemoryBuffer[advancedSharedMemoryBufferIndex];
                if (sharedMemoryOffset + sizeInBytes <= buffer.Length)
                {
                    currentSharedMemoryView = buffer.View.GetSubView(sharedMemoryOffset, sizeInBytes);
                    sharedMemoryOffset += sizeInBytes;
                    return;
                }
                // We have to perform a new buffer allocation
            }

            // We need a new dynamically-chunk of shared memory
            var tempBuffer = Accelerator.Allocate<byte>(IntrinsicMath.Max(sizeInBytes, SharedMemoryChunkSize));
            advancedSharedMemoryBuffer.Add(tempBuffer);
            currentSharedMemoryView = tempBuffer.View.GetSubView(0, sizeInBytes);

            sharedMemoryOffset = sizeInBytes;
            ++advancedSharedMemoryBufferIndex;
        }

        /// <summary>
        /// Performs a shared-memory allocation.
        /// </summary>
        /// <param name="extent">The number of elements.</param>
        /// <returns>The resolved shared-memory array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> AllocateSharedMemory<T>(int extent)
            where T : struct
        {
            var isMainThread = Interlocked.CompareExchange(ref sharedMemoryLock, 1, 0) == 0;
            if (isMainThread)
                AllocateSharedMemoryInternal<T>(extent);
            Barrier();
            var result = currentSharedMemoryView;
            if (isMainThread)
                Interlocked.Exchange(ref sharedMemoryLock, 0);
            Barrier();
            Debug.Assert(result.Length >= extent * Interop.SizeOf<T>(), "Invalid shared memory allocation");
            return result.Cast<T>();
        }

        /// <summary>
        /// This method waits for all threads to complete and
        /// resets all information that might be required for the next
        /// thread index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WaitForNextThreadIndex()
        {
            Barrier();
            Interlocked.Exchange(ref advancedSharedMemoryBufferIndex, -1);
            Interlocked.Exchange(ref sharedMemoryOffset, 0);
            Barrier();
        }

        /// <summary>
        /// Executes a broadcast operation.
        /// </summary>
        /// <param name="value">The desired group index.</param>
        /// <param name="groupIndex">The source thread index within the group.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Broadcast<T>(T value, int groupIndex)
            where T : struct
        {
            var view = broadcastBuffer.View.Cast<T>();
            if (Group.LinearIndex == groupIndex)
            {
                Debug.Assert(
                    Interop.SizeOf<T>() < broadcastBuffer.Length,
                    "Structure to large for broadcast operation");
                view[0] = value;
            }
            Barrier();
            var result = view[0];
            groupBarrier.SignalAndWait();
            return result;
        }

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
        /// <param name="groupDimension">The group dimension.</param>
        /// <param name="sharedMemSize">The required shared-memory size in bytes used by this group.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Initialize(Index3 groupDimension, int sharedMemSize)
        {
            sharedMemoryOffset = 0;
            sharedMemoryLock = 0;
            advancedSharedMemoryBufferIndex = -1;

            var groupSize = groupDimension.Size;
            var currentBarrierCount = groupBarrier.ParticipantCount;
            if (currentBarrierCount > groupSize)
                groupBarrier.RemoveParticipants(currentBarrierCount - groupSize);
            else if (currentBarrierCount < groupSize)
                groupBarrier.AddParticipants(groupSize - currentBarrierCount);

            if (sharedMemSize > 0)
                SharedMemory = sharedMemoryBuffer.Allocate<byte>(sharedMemSize);
            else
                SharedMemory = new ArrayView<byte>();
            if (sharedMemSize > SharedMemorySize)
                throw new InvalidKernelOperationException();
            currentSharedMemoryView = default;
        }

        /// <summary>
        /// Performs cleanup operations with respect to the previously allocated
        /// shared memory
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TearDown()
        {
            Interlocked.Exchange(ref advancedSharedMemoryBufferIndex, -1);
            foreach (var entry in advancedSharedMemoryBuffer)
                entry.Dispose();
            advancedSharedMemoryBuffer.Clear();
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
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                groupBarrier.Dispose();
                sharedMemoryBuffer.Dispose();
                broadcastBuffer.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
