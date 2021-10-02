// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2021 ILGPU Project
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
                var allocation = CPUMemoryBuffer.Create(
                    Parent.Multiprocessor.Accelerator,
                    Extent,
                    Interop.SizeOf<T>());

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
        /// <param name="multiprocessor">The target CPU multiprocessor.</param>
        public CPURuntimeGroupContext(CPUMultiprocessor multiprocessor)
            : base(multiprocessor)
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
        /// Executes a thread barrier.
        /// </summary>
        /// <returns>The number of participating threads.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Barrier() => Multiprocessor.GroupBarrier();

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
            where T : unmanaged =>
            PerformLocked<
                CPURuntimeGroupContext,
                GetSharedMemory<T>,
                ArrayView<T>>(
                this,
                new GetSharedMemory<T>(this, extent));

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
            Barrier();
            if (predicate)
                Interlocked.Increment(ref groupCounter);
            Barrier();
            var result = Interlocked.CompareExchange(ref groupCounter, 0, 0);
            numParticipants = Barrier();
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
