// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityMultiprocessor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a single velocity kernel processing delegate.
    /// </summary>
    /// <param name="globalStartIndex">The start index within the thread grid.</param>
    /// <param name="globalEndIndex">The end index within the thread grid.</param>
    /// <param name="parameters">The current parameters.</param>
    delegate void VelocityKernelEntryPoint(
        int globalStartIndex,
        int globalEndIndex,
        VelocityParameters parameters);

    /// <summary>
    /// A single velocity multiprocessor consisting of a single processing thread and
    /// a runtime context.
    /// </summary>
    sealed class VelocityMultiprocessor : DisposeBase
    {
        #region Static

        /// <summary>
        /// All kernel handler types required to launch a kernel delegate on this MP.
        /// </summary>
        public static readonly ImmutableArray<Type> KernelHandlerTypes =
            ImmutableArray.Create(
                typeof(int),
                typeof(int),
                typeof(VelocityParameters));

        /// <summary>
        /// Stores the current velocity multiprocessor.
        /// </summary>
        [ThreadStatic]
        private static VelocityMultiprocessor current;

        /// <summary>
        /// Returns the parent velocity multiprocessor for the current thread.
        /// </summary>
        /// <returns>The parent multiprocessor for the current thread.</returns>
        public static VelocityMultiprocessor GetCurrent() => current;

        /// <summary>
        /// Allocates a chunk of shared memory.
        /// </summary>
        /// <returns>A velocity warp made of shared-memory pointers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 GetSharedMemory<T>(int length)
            where T : unmanaged
        {
            var currentProcessor = GetCurrent();
            var sharedMemoryView = currentProcessor.GetSharedMemoryFromPool<T>(length);
            long intPtr = sharedMemoryView.LoadEffectiveAddressAsPtr().ToInt64();
            return VelocityWarp64.GetConstI(intPtr);
        }

        /// <summary>
        /// Allocates a chunk of local memory.
        /// </summary>
        /// <returns>A velocity warp made of local-memory pointers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VelocityWarp64 GetLocalMemory<T>(int length)
            where T : unmanaged
        {
            var currentProcessor = GetCurrent();
            var localMemoryView = currentProcessor.GetLocalMemoryFromPool<T>(
                length * currentProcessor.WarpSize);

            long intPtr = localMemoryView.LoadEffectiveAddressAsPtr().ToInt64();
            var addresses = VelocityWarp64.GetConstI(intPtr);
            var offsets = VelocityWarp64.GetConstI(Interop.SizeOf<T>());
            return offsets.MultiplyAddU(
                VelocityWarp64.LaneIndexVector,
                addresses);
        }

        /// <summary>
        /// Returns the current linear thread indices for all warp lanes.
        /// </summary>
        /// <returns>A velocity warp made of grid indices.</returns>
        public static VelocityWarp32 GetCurrentLinearIdx() => GetCurrent().LinearIdx;

        /// <summary>
        /// Returns the current linear thread indices for all warp lanes.
        /// </summary>
        /// <returns>A velocity warp made of grid indices.</returns>
        public static void SetCurrentLinearIdx(int linearIndex) =>
            GetCurrent().ResetLinearIndex(linearIndex);

        /// <summary>
        /// Returns the current grid indices for all warp lanes associated with this
        /// multiprocessor.
        /// </summary>
        /// <returns>A velocity warp made of grid indices.</returns>
        public static VelocityWarp32 GetCurrentGridIdx() =>
            VelocityWarp32.GetConstI(GetCurrent().GridIdx);

        /// <summary>
        /// Returns the current grid dimension for all warp lanes associated with this
        /// multiprocessor.
        /// </summary>
        /// <returns>A velocity warp made of the current grid dimension.</returns>
        public static VelocityWarp32 GetCurrentGridDim() =>
            VelocityWarp32.GetConstI(GetCurrent().GridDim);

        /// <summary>
        /// Returns the current group dimension for all warp lanes associated with this
        /// multiprocessor.
        /// </summary>
        /// <returns>A velocity warp made of the current group dimension.</returns>
        public static VelocityWarp32 GetCurrentGroupDim() =>
            VelocityWarp32.GetConstI(GetCurrentGroupDimScalar());

        /// <summary>
        /// Returns the current group dimension for all warp lanes associated with this
        /// multiprocessor.
        /// </summary>
        /// <returns>The current group dimension.</returns>
        public static int GetCurrentGroupDimScalar() => GetCurrent().GroupDim;

        /// <summary>
        /// Represents a handle to the <see cref="GetSharedMemory{T}"/> method.
        /// </summary>
        public static readonly MethodInfo GetSharedMemoryMethodInfo =
            typeof(VelocityMultiprocessor).GetMethod(
               nameof(GetSharedMemory),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Represents a handle to the <see cref="GetLocalMemory{T}"/> method.
        /// </summary>
        public static readonly MethodInfo GetLocalMemoryMethodInfo =
            typeof(VelocityMultiprocessor).GetMethod(
                nameof(GetLocalMemory),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Represents a handle to the <see cref="GetCurrentLinearIdx"/> method.
        /// </summary>
        public static readonly MethodInfo GetCurrentLinearIdxMethod =
            typeof(VelocityMultiprocessor).GetMethod(
                nameof(GetCurrentLinearIdx),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Represents a handle to the <see cref="SetCurrentLinearIdx"/> method.
        /// </summary>
        public static readonly MethodInfo SetCurrentLinearIdxMethod =
            typeof(VelocityMultiprocessor).GetMethod(
                nameof(SetCurrentLinearIdx),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Represents a handle to the <see cref="GetCurrentGridIdx"/> method.
        /// </summary>
        public static readonly MethodInfo GetCurrentGridIdxMethodInfo =
            typeof(VelocityMultiprocessor).GetMethod(
                nameof(GetCurrentGridIdx),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Represents a handle to the <see cref="GetCurrentGridDim"/> method.
        /// </summary>
        public static readonly MethodInfo GetCurrentGridDimMethodInfo =
            typeof(VelocityMultiprocessor).GetMethod(
                nameof(GetCurrentGridDim),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Represents a handle to the <see cref="GetCurrentGroupDim"/> method.
        /// </summary>
        public static readonly MethodInfo GetCurrentGroupDimMethodInfo =
            typeof(VelocityMultiprocessor).GetMethod(
                nameof(GetCurrentGroupDim),
                BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Represents a handle to the <see cref="GetCurrentGroupDimScalar"/> method.
        /// </summary>
        public static readonly MethodInfo GetCurrentGroupDimScalarMethodInfo =
            typeof(VelocityMultiprocessor).GetMethod(
                nameof(GetCurrentGroupDimScalar),
                BindingFlags.Public | BindingFlags.Static);

        #endregion

        #region Events

        /// <summary>
        /// Will be raised once a chunk of a scheduled thread grid has been completed.
        /// </summary>
        public Action<VelocityMultiprocessor> ProcessingCompleted;

        #endregion

        #region Instance

        // Thread data
        private readonly Thread runtimeThread;
        private readonly SemaphoreSlim startProcessingSema;

        // Context data
        private readonly VelocityMemoryBufferPool sharedMemoryPool;
        private readonly VelocityMemoryBufferPool localMemoryPool;

        // Runtime data
        private volatile VelocityKernelEntryPoint kernelHandler;
        private volatile int startIndexRange;
        private volatile int endIndexRange;
        private volatile VelocityParameters kernelParameters;
        private volatile bool running = true;

        /// <summary>
        /// Initializes a new velocity multiprocessor.
        /// </summary>
        /// <param name="accelerator">The parent velocity accelerator.</param>
        /// <param name="processorIndex">The current processor index.</param>
        internal VelocityMultiprocessor(
            VelocityAccelerator accelerator,
            int processorIndex)
        {
            runtimeThread = new Thread(DoWork)
            {
                Priority = accelerator.ThreadPriority,
                IsBackground = true,
                Name = $"ILGPU_{accelerator.InstanceId}_Velocity_{processorIndex}"
            };

            startProcessingSema = new SemaphoreSlim(0);
            sharedMemoryPool = new VelocityMemoryBufferPool(
                accelerator,
                accelerator.MaxSharedMemoryPerGroup);
            localMemoryPool = new VelocityMemoryBufferPool(
                accelerator,
                accelerator.MaxLocalMemoryPerThread);
            WarpSize = accelerator.WarpSize;
            ProcessorIndex = processorIndex;

            runtimeThread.Start();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current warp size.
        /// </summary>
        public int WarpSize { get; }

        /// <summary>
        /// Returns the multiprocessor index.
        /// </summary>
        public int ProcessorIndex { get; }

        /// <summary>
        /// Returns the precomputed grid indices for all lanes in the current
        /// multiprocessor.
        /// </summary>
        public VelocityWarp32 LinearIdx { get; private set; }

        /// <summary>
        /// Returns the precomputed grid indices for all lanes in the current
        /// multiprocessor.
        /// </summary>
        public int GridIdx { get; private set; }

        /// <summary>
        /// Returns the current grid dimension.
        /// </summary>
        public int GridDim { get; private set; }

        /// <summary>
        /// Returns the current group dimension.
        /// </summary>
        public int GroupDim { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Resets the current linear index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetLinearIndex(int linearIndex)
        {
            LinearIdx =
                VelocityWarp32.LaneIndexVector.AddU(
                VelocityWarp32.GetConstI(linearIndex));
            GridIdx = linearIndex / GroupDim;
        }

        /// <summary>
        /// Gets a chunk of shared memory of a certain type.
        /// </summary>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The element type to allocate.</typeparam>
        /// <returns>A view pointing to the right chunk of shared memory.</returns>
        public ArrayView<T> GetSharedMemoryFromPool<T>(int length)
            where T : unmanaged =>
            sharedMemoryPool.Allocate<T>(length);

        /// <summary>
        /// Gets a chunk of local memory of a certain type.
        /// </summary>
        /// <param name="length">The number of elements.</param>
        /// <typeparam name="T">The element type to allocate.</typeparam>
        /// <returns>A view pointing to the right chunk of local memory.</returns>
        public ArrayView<T> GetLocalMemoryFromPool<T>(int length)
            where T : unmanaged =>
            localMemoryPool.Allocate<T>(length);

        /// <summary>
        /// Dispatches a new kernel execution.
        /// </summary>
        /// <param name="handler">The kernel handler delegate.</param>
        /// <param name="startIndex">The start interval index.</param>
        /// <param name="endIndex">The end interval index.</param>
        /// <param name="gridDimension">The current grid dimension.</param>
        /// <param name="groupDimension">The current group dimension.</param>
        /// <param name="parameters">All kernel parameters.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Run(
            VelocityKernelEntryPoint handler,
            int startIndex,
            int endIndex,
            int gridDimension,
            int groupDimension,
            VelocityParameters parameters)
        {
            GridDim = gridDimension;
            GroupDim = groupDimension;

            // Note that we do not have to invoke
            // ResetGridIndex(offset: ...);
            // here, as this method will be automatically invoked by each Velocity kernel

            // Schedule this operation
            kernelHandler = handler;
            startIndexRange = startIndex;
            endIndexRange = endIndex;
            kernelParameters = parameters;
            sharedMemoryPool.Reset();
            localMemoryPool.Reset();

            // Ensure visibility of all changes to other threads
            Thread.MemoryBarrier();

            // Launch the processing task
            startProcessingSema.Release();
        }

        /// <summary>
        /// The main processing thread of this multiprocessor.
        /// </summary>
        private void DoWork()
        {
            // Assign the current multiprocessor to this instance
            current = this;

            // Process all tasks
            while (true)
            {
                // Wait for the next task to arrive
                startProcessingSema.Wait();

                // Break the loop if we are shutting down
                if (!running)
                    break;

                // Launch the actual kernel method
                kernelHandler(startIndexRange, endIndexRange, kernelParameters);

                // Signal the main thread that the processing has been completed. Note
                // that we avoid any null checks at this point
                ProcessingCompleted(this);
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Waits for the processing thread to shutdown and disposes all internal thread
        /// objects.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                running = false;
                startProcessingSema.Release();
                runtimeThread.Join();

                startProcessingSema.Dispose();
                sharedMemoryPool.Dispose();
                localMemoryPool.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
