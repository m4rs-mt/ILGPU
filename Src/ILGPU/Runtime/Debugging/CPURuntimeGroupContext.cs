// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPURuntimeGroupContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a runtime context for thread groups.
    /// </summary>
    sealed class CPURuntimeGroupContext : CPURuntimeContext, CPURuntimeContext.IParent
    {
        #region Thread Static

        /// <summary>
        /// Represents the current context.
        /// </summary>
        [ThreadStatic]
        private static CPURuntimeGroupContext? currentContext;

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
                return currentContext.AsNotNull();
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// A counter for the computation of interlocked group counters.
        /// </summary>
        private volatile int groupCounter;

        /// <summary>
        /// Group-wide accumulator for group allocation indices.
        /// </summary>
        private int groupAllocationIndexAccumulator;

        /// <summary>
        /// Internal storage to track group-wide allocation indices
        /// </summary>
        private readonly int[] groupAllocationIndices;

        /// <summary>
        /// The current dynamic shared memory array size in bytes.
        /// </summary>
        private volatile int dynamicSharedMemoryArrayLength;

        /// <summary>
        /// A temporary cache for additional shared memory requirements.
        /// </summary>
        /// <remarks>
        /// Note that these buffers are only required for debug CPU builds. In
        /// these cases, we cannot move nested
        /// <see cref="SharedMemory.Allocate{T}(int)"/> instructions out of nested loops
        /// to provide the best debugging experience.
        /// </remarks>
        private InlineList<CPUMemoryBuffer?> sharedMemory =
            InlineList<CPUMemoryBuffer?>.Create(16);

        /// <summary>
        /// Shared-memory allocation lock object for synchronizing accesses to the
        /// <see cref="sharedMemory" /> list.
        /// </summary>
        private readonly object sharedMemoryLock = new object();

        /// <summary>
        /// Constructs a new CPU-based runtime context for parallel processing.
        /// </summary>
        /// <param name="multiprocessor">The target CPU multiprocessor.</param>
        public CPURuntimeGroupContext(CPUMultiprocessor multiprocessor)
            : base(multiprocessor)
        {
            groupAllocationIndices = new int[multiprocessor.MaxNumThreadsPerGroup];
        }

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
        /// Executes a thread barrier.
        /// </summary>
        /// <returns>The number of participating threads.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BlockBarrier() => Multiprocessor.GroupBarrier();

        /// <summary>
        /// The internal implementation of a group barrier that takes current allocation
        /// indices into account.
        /// </summary>
        /// <returns>The number of participating threads.</returns>
        private int GroupAllocationSynchronizedBarrier()
        {
            // Determine current index and maximize across all threads in the group
            var context = CPURuntimeThreadContext.Current;
            ref int currentIndex = ref groupAllocationIndices[context.LinearGroupIndex];

            // Accumulate results and perform the actual barrier
            Atomic.Max(ref groupAllocationIndexAccumulator, currentIndex);
            int count1 = BlockBarrier();

            // Get the actual result as long as it is available and update our own counter
            currentIndex = Atomic.CompareExchange(
                ref groupAllocationIndexAccumulator,
                -1,
                -1);

            // Wait for all threads to get to this point in order to avoid disturbances
            // that may affect the group accumulation counter
            int count2 = BlockBarrier();
            Debug.Assert(count1 == count2, "Lost threads within group barriers");
            return count2;
        }

        /// <summary>
        /// Executes a thread barrier.
        /// </summary>
        /// <returns>The number of participating threads.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Barrier()
        {
            // Wait without any modifications
            BlockBarrier();

            // Perform the actual group sync barrier
            return GroupAllocationSynchronizedBarrier();
        }

        /// <summary>
        /// Performs a local-memory allocation.
        /// </summary>
        /// <returns>The resolved local-memory array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> AllocateLocalMemory<T>(int extent)
            where T : unmanaged
        {
            var buffer = CPUMemoryBuffer.Create(
                Multiprocessor.Accelerator,
                extent,
                Interop.SizeOf<T>());
            return new ArrayView<T>(buffer, 0, extent);
        }

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
            where T : unmanaged
        {
            var context = CPURuntimeThreadContext.Current;
            int nextIndex = groupAllocationIndices[context.LinearGroupIndex]++;

            // Perform synchronized
            lock (sharedMemoryLock)
            {
                // Register buffers
                while (sharedMemory.Count <= nextIndex)
                    sharedMemory.Add(null);

                // Allocate the requested amount of elements
                ref var buffer = ref sharedMemory[nextIndex];
                if (buffer is null)
                {
                    buffer = CPUMemoryBuffer.Create(
                        Multiprocessor.Accelerator,
                        extent,
                        Interop.SizeOf<T>());
                }

                Thread.MemoryBarrier();
                // Publish the allocated shared memory source
                return new ArrayView<T>(buffer, 0, extent);
            }
        }

        /// <summary>
        /// Executes a thread barrier and returns the number of threads for which
        /// the predicate evaluated to true.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <param name="numParticipants">The number of participants.</param>
        /// <returns>
        /// The number of threads for which the predicate evaluated to true.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BarrierPopCount(bool predicate, out int numParticipants)
        {
            Interlocked.Exchange(ref groupCounter, 0);
            BlockBarrier();
            if (predicate)
                Interlocked.Increment(ref groupCounter);
            GroupAllocationSynchronizedBarrier();
            var result = Interlocked.CompareExchange(ref groupCounter, 0, 0);
            numParticipants = BlockBarrier();
            return result;
        }

        /// <summary>
        /// Executes a thread barrier and returns the number of threads for which
        /// the predicate evaluated to true.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>
        /// The number of threads for which the predicate evaluated to true.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BarrierPopCount(bool predicate) =>
            BarrierPopCount(predicate, out int _);

        /// <summary>
        /// Executes a thread barrier and returns true if all threads in a block
        /// fulfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, if all threads in a block fulfills the predicate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BarrierAnd(bool predicate) =>
            BarrierPopCount(predicate, out int numParticipants) == numParticipants;

        /// <summary>
        /// Executes a thread barrier and returns true if any thread in a block
        /// fulfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, if any thread in a block fulfills the predicate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BarrierOr(bool predicate) =>
            BarrierPopCount(predicate, out int _) > 0;

        /// <summary>
        /// Executes a broadcast operation.
        /// </summary>
        /// <typeparam name="T">The element type to broadcast.</typeparam>
        /// <param name="value">The desired group index.</param>
        /// <param name="groupIndex">The source thread index within the group.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Broadcast<T>(T value, int groupIndex)
            where T : unmanaged =>
            Broadcast(
                this,
                value,
                CPURuntimeThreadContext.Current.LinearGroupIndex,
                groupIndex);

        /// <summary>
        /// Initializes this context.
        /// </summary>
        /// <param name="gridDimension">The grid dimension.</param>
        /// <param name="groupDimension">The group dimension.</param>
        /// <param name="sharedMemoryConfig">
        /// The current shared memory configuration.
        /// </param>
        public void Initialize(
            in Index3D gridDimension,
            in Index3D groupDimension,
            in SharedMemoryConfig sharedMemoryConfig)
        {
            GridDimension = gridDimension;
            GroupDimension = groupDimension;
            GroupSize = groupDimension.Size;
            dynamicSharedMemoryArrayLength = sharedMemoryConfig.NumElements;

            ClearSharedMemoryAllocations();
            Initialize();
        }

        /// <summary>
        /// Performs cleanup operations with respect to the previously allocated
        /// shared memory
        /// </summary>
        public void TearDown() => ClearSharedMemoryAllocations();

        /// <summary>
        /// Clears all previously allocated shared-memory operations.
        /// </summary>
        private void ClearSharedMemoryAllocations()
        {
            foreach (var entry in sharedMemory)
                entry?.Dispose();
            sharedMemory.Clear();
            Array.Clear(groupAllocationIndices, 0, groupAllocationIndices.Length);
            Atomic.Exchange(ref groupAllocationIndexAccumulator, 0);
        }

        /// <summary>
        /// Makes the current context the active one for this thread.
        /// </summary>
        internal void MakeCurrent() => currentContext = this;

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
}
