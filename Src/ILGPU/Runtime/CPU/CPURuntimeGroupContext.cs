// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPURuntimeGroupContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a runtime context for thread groups.
    /// </summary>
    abstract class CPURuntimeGroupContext : CPURuntimeContext
    {
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
                Trace.Assert(
                    currentContext != null,
                    ErrorMessages.InvalidKernelOperation);
                return currentContext;
            }
        }

        protected static int GroupIndex =>
            CPURuntimeThreadContext.Current.LinearGroupIndex;

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents an operation that allocates and managed shared memory.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        private readonly struct GetSharedMemory<T> : ILockedOperation<ArrayView<T>>
            where T : unmanaged
        {
            public GetSharedMemory(CPURuntimeGroupContext parent, long extent)
            {
                Parent = parent;
                Extent = extent;
            }

            /// <summary>
            /// Returns the parent context.
            /// </summary>
            public CPURuntimeGroupContext Parent { get; }

            /// <summary>
            /// Returns the number of elements to allocate.
            /// </summary>
            public long Extent { get; }

            /// <summary>
            /// Allocates a new chunk of shared memory.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void ApplySyncInMainThread()
            {
                // Allocate the requested amount of elements
                var allocation = CPUMemoryBuffer.Create(Extent, Interop.SizeOf<T>());

                // Publish the allocated shared memory source
                Parent.currentSharedMemorySource = allocation;
                Parent.sharedMemory.Add(allocation);
            }

            /// <summary>
            /// Returns the least recently allocated chunk of shared memory.
            /// </summary>
            public readonly ArrayView<T> Result =>
                new ArrayView<T>(Parent.currentSharedMemorySource, 0, Extent);
        }

        #endregion

        #region Instance

        /// <summary>
        /// A counter for the computation of interlocked group counters.
        /// </summary>
        private volatile int groupCounter;

        /// <summary>
        /// The current dynamic shared memory array size in bytes.
        /// </summary>
        private volatile int dynamicSharedMemoryArrayLength;

        /// <summary>
        /// A temporary cache for additional shared memory requirements.
        /// </summary>
        /// <remarks>
        /// Note that this buffer is only required for debug CPU builds. In
        /// these cases, we cannot move nested
        /// <see cref="SharedMemory.Allocate{T}(int)"/> instructions out of nested loops
        /// to provide the best debugging experience.
        /// </remarks>
        private InlineList<CPUMemoryBuffer> sharedMemory =
            InlineList<CPUMemoryBuffer>.Create(16);

        /// <summary>
        /// A currently active unmanaged memory source.
        /// </summary>
        private volatile CPUMemoryBuffer currentSharedMemorySource;

        /// <summary>
        /// Constructs a new CPU-based runtime context for parallel processing.
        /// </summary>
        /// <param name="accelerator">The target CPU accelerator.</param>
        public CPURuntimeGroupContext(CPUAccelerator accelerator)
            : base(accelerator)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the group dimension of the scheduled thread grid.
        /// </summary>
        public Index3D GridDimension { get; private set; }

        /// <summary>
        /// Returns the group dimension of the scheduled thread grid.
        /// </summary>
        public Index3D GroupDimension { get; private set; }

        /// <summary>
        /// Returns the current total group size in number of threads.
        /// </summary>
        public int GroupSize { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Performs a dynamic shared-memory allocation.
        /// </summary>
        /// <returns>The resolved shared-memory array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> AllocateSharedMemoryDynamic<T>()
            where T : unmanaged =>
            AllocateSharedMemory<T>(dynamicSharedMemoryArrayLength);

        /// <summary>
        /// Performs a shared-memory allocation.
        /// </summary>
        /// <param name="extent">The number of elements.</param>
        /// <returns>The resolved shared-memory array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> AllocateSharedMemory<T>(int extent)
            where T : unmanaged =>
            PerformLocked<
                GetSharedMemory<T>,
                ArrayView<T>>(
                new GetSharedMemory<T>(this, extent));

        /// <summary>
        /// Executes a thread barrier and returns the number of threads for which
        /// the predicate evaluated to true.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>
        /// The number of threads for which the predicate evaluated to true.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BarrierPopCount(bool predicate)
        {
            Interlocked.Exchange(ref groupCounter, 0);
            Barrier();
            if (predicate)
                Interlocked.Increment(ref groupCounter);
            Barrier();
            var result = Interlocked.CompareExchange(ref groupCounter, 0, 0);
            Barrier();
            return result;
        }

        /// <summary>
        /// Executes a thread barrier and returns true if all threads in a block
        /// fulfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, if all threads in a block fulfills the predicate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BarrierAnd(bool predicate) =>
            BarrierPopCount(predicate) == NumParticipants;

        /// <summary>
        /// Executes a thread barrier and returns true if any thread in a block
        /// fulfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, if any thread in a block fulfills the predicate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BarrierOr(bool predicate) =>
            BarrierPopCount(predicate) > 0;

        /// <summary>
        /// Initializes this context.
        /// </summary>
        /// <param name="gridDimension">The grid dimension.</param>
        /// <param name="groupDimension">The group dimension.</param>
        /// <param name="sharedMemoryConfig">
        /// The current shared memory configuration.
        /// </param>
        public virtual void Initialize(
            in Index3D gridDimension,
            in Index3D groupDimension,
            in SharedMemoryConfig sharedMemoryConfig)
        {
            GridDimension = gridDimension;
            GroupDimension = groupDimension;
            GroupSize = groupDimension.Size;
            dynamicSharedMemoryArrayLength = sharedMemoryConfig.NumElements;

            ClearSharedMemoryAllocations();
            Initialize(groupDimension.Size);
        }

        /// <summary>
        /// Performs cleanup operations with respect to the previously allocated
        /// shared memory
        /// </summary>
        public virtual void TearDown() => ClearSharedMemoryAllocations();

        /// <summary>
        /// Begins processing of the current thread.
        /// </summary>
        public abstract void BeginThreadProcessing();

        /// <summary>
        /// Ends a previously started processing task of the current thread.
        /// </summary>
        public abstract void EndThreadProcessing();

        /// <summary>
        /// Finishes processing of the current thread.
        /// </summary>
        public abstract void FinishThreadProcessing();

        /// <summary>
        /// Clears all previously allocated shared-memory operations.
        /// </summary>
        private void ClearSharedMemoryAllocations()
        {
            currentSharedMemorySource = null;
            foreach (var entry in sharedMemory)
                entry.Dispose();
            sharedMemory.Clear();
        }

        /// <summary>
        /// Makes the current context the active one for this thread.
        /// </summary>
        internal void MakeCurrent() => currentContext = this;

        #endregion

        #region Task Runtime

        /// <summary>
        /// This method does not perform any operation at the moment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void BeginParallelThreadProcessing() { }

        /// <summary>
        /// This method waits for all threads in this group to complete.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EndParallelThreadProcessing() => Barrier();

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ClearSharedMemoryAllocations();
            base.Dispose(disposing);
        }

        #endregion
    }

    sealed class SequentialCPURuntimeGroupContext : CPURuntimeGroupContext
    {
        /// <summary>
        /// The internal activity set.
        /// </summary>
        private volatile BitArray activitySet = new BitArray(1024);

        /// <summary>
        /// The internal index of the currently active thread.
        /// </summary>
        private volatile int activeThreadIndex;

        public SequentialCPURuntimeGroupContext(CPUAccelerator accelerator)
            : base(accelerator)
        { }

        /// <inheritdoc/>
        public override void Initialize(
            in Index3D gridDimension,
            in Index3D groupDimension,
            in SharedMemoryConfig sharedMemoryConfig)
        {
            // Setup the activity set
            int groupSize = groupDimension.Size;
            if (activitySet.Length < groupSize)
                activitySet = new BitArray(groupSize);

            // Mark all threads as active and activate the first one
            activitySet.SetAll(true);
            activeThreadIndex = 0;

            // Setup remaining information and issue a thread barrier
            base.Initialize(gridDimension, groupDimension, sharedMemoryConfig);
        }

        /// <summary>
        /// Puts the current thread into sleep mode (if there are some other threads
        /// being active) and wakes up the next thread.
        /// </summary>
        private void ScheduleNextThread()
        {
            // Determine the next thread that might become active
            int groupIdx = GroupIndex;
            lock (activitySet.SyncRoot)
            {
                // Determine the next thread that might become active
                for (int i = 1; i < GroupSize; ++i)
                {
                    int index = (groupIdx + i) % GroupSize;
                    if (activitySet[index])
                    {
                        // This thread can become active
                        activeThreadIndex = index;
                        Monitor.PulseAll(activitySet.SyncRoot);
                        break;
                    }
                }
            }
            Thread.MemoryBarrier();
        }

        public override void Barrier() =>
            // We have hit an inter-group thread barrier that requires all other threads
            // to run until this point
            ScheduleNextThread();

        /// <inheritdoc/>
        public override void BeginThreadProcessing()
        {
            // NOTE: the current thread lock cannot be null
            int groupIdx = GroupIndex;

            lock (activitySet.SyncRoot)
            {
                // Wait for our thread to become active
                while (activeThreadIndex != groupIdx)
                    Monitor.Wait(activitySet.SyncRoot);

                // We have become active... continue processing
            }
        }

        /// <inheritdoc/>
        public override void EndThreadProcessing() => ScheduleNextThread();

        /// <inheritdoc/>
        public override void FinishThreadProcessing()
        {
            // Remove the current thread from the set
            lock (activitySet.SyncRoot)
                activitySet.Set(GroupIndex, false);
        }
    }

    sealed class ParallelCPURuntimeGroupContext : CPURuntimeGroupContext
    {
        public ParallelCPURuntimeGroupContext(CPUAccelerator accelerator)
            : base(accelerator)
        { }

        private void ParallelBarrier() => WaitForAllThreads();

        public override void Barrier() => ParallelBarrier();

        /// <summary>
        /// This method does not perform any operation at the moment.
        /// </summary>
        public override void BeginThreadProcessing() { }

        /// <summary>
        /// This method waits for all threads in this group to complete.
        /// </summary>
        public override void EndThreadProcessing() => ParallelBarrier();

        /// <inheritdoc/>
        public override void FinishThreadProcessing() => RemoveBarrierParticipant();
    }
}
