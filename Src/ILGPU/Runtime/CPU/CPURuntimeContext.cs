﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPURuntimeContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a runtime context for a single group.
    /// </summary>
    abstract class CPURuntimeContext : DisposeBase
    {
        #region Nested Types

        /// <summary>
        /// Represents an operation that is performed by a single "main" thread in
        /// the scope of a parallel CPU runtime execution.
        /// </summary>
        /// <typeparam name="T">The result type of this operation.</typeparam>
        protected interface ILockedOperation<T>
        {
            /// <summary>
            /// Applies the current operation in sync with all other threads.
            /// </summary>
            void ApplySyncInMainThread();

            /// <summary>
            /// Retrieves the global result of the operation.
            /// </summary>
            /// <remarks>
            /// Note that this getter will be executed by all other threads in the
            /// group in parallel.
            /// </remarks>
            T Result { get; }
        }

        /// <summary>
        /// A parent object that has a barrier function.
        /// </summary>
        public interface IParent
        {
            /// <summary>
            /// Executes a thread barrier.
            /// </summary>
            /// <returns>The number of participating threads.</returns>
            int Barrier();
        }

        /// <summary>
        /// Represents an operation that allocates and managed broadcast memory.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        protected readonly struct GetBroadcastMemory<T> : ILockedOperation<ArrayView<T>>
            where T : unmanaged
        {
            /// <summary>
            /// Constructs a new allocation operation.
            /// </summary>
            public GetBroadcastMemory(CPURuntimeContext parent, int groupIndex)
            {
                Parent = parent;
                GroupIndex = groupIndex;
            }

            /// <summary>
            /// Returns the parent context.
            /// </summary>
            public CPURuntimeContext Parent { get; }

            /// <summary>
            /// Returns the current group index to read from.
            /// </summary>
            public int GroupIndex { get; }

            /// <summary>
            /// Returns a reference to the parent broadcast cache.
            /// </summary>
            public readonly CPUMemoryBufferCache BroadcastBuffer =>
                Parent.broadcastBuffer;

            /// <summary>
            /// Allocates the required broadcast memory.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void ApplySyncInMainThread()
            {
                // Allocate at least the amount of memory require to store a single
                // element to perform the exchange operation
                BroadcastBuffer.Allocate<T>(1);

                // Setup the global group index
                Parent.broadcastIndex = GroupIndex;
            }

            /// <summary>
            /// Returns a view to the (potentially) adjusted broadcast cache.
            /// </summary>
            public readonly ArrayView<T> Result => BroadcastBuffer.Cache.Cast<T>();
        }

        #endregion

        #region Instance

        /// <summary>
        /// The global memory lock variable.
        /// </summary>
        private volatile int memoryLock;

        /// <summary>
        /// A temporary location for broadcast values.
        /// </summary>
        private readonly CPUMemoryBufferCache broadcastBuffer;

        /// <summary>
        /// A temporary location for broadcast indices.
        /// </summary>
        private volatile int broadcastIndex;

        /// <summary>
        /// Constructs a new CPU-based runtime context for parallel processing.
        /// </summary>
        /// <param name="multiprocessor">The target CPU multiprocessor.</param>
        protected CPURuntimeContext(CPUMultiprocessor multiprocessor)
        {
            Multiprocessor = multiprocessor
                ?? throw new ArgumentNullException(nameof(multiprocessor));

            broadcastBuffer = new CPUMemoryBufferCache(multiprocessor.Accelerator);
            broadcastBuffer.Allocate<int>(16);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent multiprocessor instance.
        /// </summary>
        public CPUMultiprocessor Multiprocessor { get; }

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public CPUAccelerator Accelerator => Multiprocessor.Accelerator;

        #endregion

        #region Methods

        /// <summary>
        /// Performs the given operation locked with respect to all other operating
        /// threads that are currently active.
        /// </summary>
        /// <typeparam name="TParent">The parent object type.</typeparam>
        /// <typeparam name="TOperation">The operation type.</typeparam>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="parent">The parent object instance.</param>
        /// <param name="operation">The operation to perform.</param>
        /// <returns>The determined result value for all threads.</returns>
        /// <remarks>
        /// It internally acquires a lock using <see cref="AquireLock"/> and determines
        /// a "main thread" that can execute the given operation in sync with all
        /// other threads. Afterwards, all threads continue and query the result of
        /// the synchronized operation and the main thread releases its lock.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T PerformLocked<TParent, TOperation, T>(
            TParent parent,
            TOperation operation)
            where TParent : IParent
            where TOperation : ILockedOperation<T>
        {
            bool isMainThread = AquireLock();
            if (isMainThread)
                operation.ApplySyncInMainThread();
            parent.Barrier();
            var result = operation.Result;
            ReleaseLock(parent, isMainThread);
            return result;
        }

        /// <summary>
        /// Acquires the internal thread lock and returns true, if the current thread
        /// becomes the main thread that can perform thread-safe operations.
        /// </summary>
        /// <returns>True, if the current thread is the main thread.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool AquireLock() =>
            Interlocked.CompareExchange(ref memoryLock, 1, 0) == 0;

        /// <summary>
        /// Release the internal lock.
        /// </summary>
        /// <typeparam name="TParent">The parent object type.</typeparam>
        /// <param name="parent">The parent object instance.</param>
        /// <param name="isMainThread">True, if this thread is the main thread.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ReleaseLock<TParent>(
            TParent parent,
            bool isMainThread)
            where TParent : IParent
        {
            // If we are the main thread, release the lock by issuing an atomic
            // exchange operation in order to be visible by another AquireLock
            // operation that might be executed in the future.
            if (isMainThread)
                Interlocked.Exchange(ref memoryLock, 0);
            parent.Barrier();
        }

        /// <summary>
        /// Executes a broadcast operation.
        /// </summary>
        /// <typeparam name="TParent">The parent object type.</typeparam>
        /// <typeparam name="T">The element type to broadcast.</typeparam>
        /// <param name="parent">The parent object instance.</param>
        /// <param name="value">The desired group index.</param>
        /// <param name="threadIndex">The current thread index.</param>
        /// <param name="groupIndex">The source thread index within the group.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T Broadcast<TParent, T>(
            TParent parent,
            T value,
            int threadIndex,
            int groupIndex)
            where TParent : IParent
            where T : unmanaged
        {
            // Allocate a compatible view to perform the actual broadcast operation
            var view = PerformLocked<
                TParent,
                GetBroadcastMemory<T>,
                ArrayView<T>>(
                parent,
                new GetBroadcastMemory<T>(this, groupIndex));

            // Fill the shared view and verify the passed group index
            Trace.Assert(
                broadcastIndex == groupIndex,
                "Broadcast lanes must be the same for all participating threads");
            if (threadIndex == groupIndex)
                view[0] = value;
            parent.Barrier();

            // Get the actual broadcast result
            var result = view[0];
            parent.Barrier();
            return result;
        }

        /// <summary>
        /// Initializes the internal runtime context.
        /// </summary>
        protected void Initialize() =>
            // Initialize the lock using the interlocked API the ensure visibility
            // across all participants
            Interlocked.Exchange(ref memoryLock, 0);

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                broadcastBuffer.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
