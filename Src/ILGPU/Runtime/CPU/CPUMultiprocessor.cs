// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUMultiprocessor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ThreadState = System.Threading.ThreadState;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents an abstract GPU multiprocessor simulated on the CPU.
    /// </summary>
    abstract partial class CPUMultiprocessor : DisposeBase
    {
        #region Static

        /// <summary>
        /// Creates a new CPU multiprocessor instance.
        /// </summary>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="processorIndex">The index of the multiprocessor.</param>
        /// <param name="usesSequentialProcessing">
        /// True, if this multiprocessor uses a sequential execution policy that executes
        /// a single thread at a time to improve the debugging experience.
        /// </param>
        /// <returns>The created multiprocessor.</returns>
        public static CPUMultiprocessor Create(
            CPUAccelerator accelerator,
            int processorIndex,
            bool usesSequentialProcessing) =>
            usesSequentialProcessing
            ? new SequentialProcessor(accelerator, processorIndex) as CPUMultiprocessor
            : new ParallelProcessor(accelerator, processorIndex);

        #endregion

        #region Instance

        // Execution threads

        private readonly Thread[] threads;
        private readonly Barrier processorBarrier = new Barrier(0);
        private readonly CPURuntimeGroupContext groupContext;
        private readonly CPURuntimeWarpContext[] warpContexts;

        // General execution management

        private volatile int maxNumLaunchedThreadsPerGroup;

        /// <summary>
        /// Creates a new CPU multiprocessor.
        /// </summary>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="processorIndex">The index of the multiprocessor.</param>
        protected CPUMultiprocessor(CPUAccelerator accelerator, int processorIndex)
        {
            Accelerator = accelerator;
            ProcessorIndex = processorIndex;

            // Setup all warp and group contexts
            NumWarpsPerMultiprocessor = MaxNumThreadsPerMultiprocessor / WarpSize;
            warpContexts = new CPURuntimeWarpContext[NumWarpsPerMultiprocessor];
            groupContext = new CPURuntimeGroupContext(this);
            for (int i = 0; i < NumWarpsPerMultiprocessor; ++i)
                warpContexts[i] = new CPURuntimeWarpContext(this, WarpSize);

            // Instantiate all runtime threads
            threads = new Thread[MaxNumThreadsPerMultiprocessor];
            Parallel.For(0, MaxNumThreadsPerMultiprocessor, i =>
            {
                var thread = threads[i] = new Thread(ExecuteThread)
                {
                    IsBackground = true,
                    Priority = Accelerator.ThreadPriority,
                };
                thread.Name = $"ILGPU_{Accelerator.InstanceId}_CPU_{ProcessorIndex}_{i}";
            });

            // Start or delay the creation of runtime threads
            if (MaxNumThreadsPerMultiprocessor <= 32)
                StartOrContinueRuntimeThreads(MaxNumThreadsPerMultiprocessor);
            else
                maxNumLaunchedThreadsPerGroup = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public CPUAccelerator Accelerator { get; }

        /// <summary>
        /// The maximum number of threads on this multiprocessor.
        /// </summary>
        public int MaxNumThreadsPerMultiprocessor =>
            Accelerator.MaxNumThreadsPerMultiprocessor;

        /// <summary>
        /// The maximum number of threads per group.
        /// </summary>
        public int MaxNumThreadsPerGroup => Accelerator.MaxNumThreadsPerGroup;

        /// <summary>
        /// Returns the warp size of this multiprocessor.
        /// </summary>
        public int WarpSize => Accelerator.WarpSize;

        /// <summary>
        /// Returns the number of warps per multiprocessor.
        /// </summary>
        public int NumWarpsPerMultiprocessor { get; }

        /// <summary>
        /// Returns the processor index.
        /// </summary>
        public int ProcessorIndex { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Starts all required runtime threads.
        /// </summary>
        /// <param name="groupSize">The group size to use.</param>
        private void StartOrContinueRuntimeThreads(int groupSize)
        {
            if (maxNumLaunchedThreadsPerGroup >= groupSize)
                return;

            // Adjust number of threads per MP
            processorBarrier.AddParticipants(
                groupSize - maxNumLaunchedThreadsPerGroup);

            // Launch all threads that we need for processing
            for (
                int threadIdx = maxNumLaunchedThreadsPerGroup;
                threadIdx < groupSize;
                ++threadIdx)
            {
                int globalThreadIdx = ProcessorIndex * MaxNumThreadsPerMultiprocessor
                    + threadIdx;
                threads[threadIdx].Start(globalThreadIdx);
            }

            // Update the number of launched threads
            maxNumLaunchedThreadsPerGroup = groupSize;
        }

        /// <summary>
        /// Setups all runtime classes related to <see cref="CPURuntimeGroupContext"/>
        /// and <see cref="CPURuntimeWarpContext"/>.
        /// </summary>
        /// <param name="task">The current CPU task.</param>
        private void SetupRuntimeClasses(CPUAcceleratorTask task)
        {
            // Setup groups contexts
            int groupSize = task.GroupDim.Size;
            int numWarps = IntrinsicMath.DivRoundUp(groupSize, WarpSize);

            if (numWarps * WarpSize > MaxNumThreadsPerGroup)
            {
                throw new NotSupportedException(string.Format(
                    RuntimeErrorMessages.NotSupportedTotalGroupSize,
                    MaxNumThreadsPerGroup));
            }

            // Initialize the associated group context
            groupContext.Initialize(
                task.GridDim,
                task.GroupDim,
                task.DynamicSharedMemoryConfig);

            // Initialize each involved warp context
            for (int i = 0, e = numWarps - 1; i < e; ++i)
                warpContexts[i].Initialize(WarpSize);

            int lastWarpSize = groupSize % WarpSize == 0
                ? WarpSize
                : groupSize % WarpSize;
            warpContexts[numWarps - 1].Initialize(lastWarpSize);
        }

        /// <summary>
        /// Initializes the launch process of the given task.
        /// </summary>
        /// <param name="task">The task to launch.</param>
        public void InitLaunch(CPUAcceleratorTask task)
        {
            Debug.Assert(task != null, "Invalid accelerator task");

            SetupRuntimeClasses(task);
            StartOrContinueRuntimeThreads(task.GroupDim.Size);
            BeginLaunch(task);
        }

        /// <summary>
        /// Finishes a kernel launch.
        /// </summary>
        public void FinishLaunch() => groupContext.TearDown();

        /// <summary>
        /// Entry point for a single processing thread.
        /// </summary>
        /// <param name="arg">The absolute thread index.</param>
        private void ExecuteThread(object arg)
        {
            // Get the current thread information
            int absoluteThreadIndex = (int)arg;
            int threadIdx = absoluteThreadIndex % MaxNumThreadsPerMultiprocessor;

            bool isMainThread = threadIdx == 0;

            // Setup a new thread context for this thread and initialize the lane index
            int laneIdx = threadIdx % WarpSize;
            int warpIdx = threadIdx / WarpSize;
            var threadContext = new CPURuntimeThreadContext(laneIdx, warpIdx)
            {
                LinearGroupIndex = threadIdx
            };
            threadContext.MakeCurrent();

            // Setup the current warp context as it always stays the same
            var warpContext = warpContexts[warpIdx];
            warpContext.MakeCurrent();

            // Setup the current group context as it always stays the same
            groupContext.MakeCurrent();

            CPUAcceleratorTask task = null;
            for (; ; )
            {
                // Get a new task to execute (if any)
                if (!Accelerator.WaitForTask(ref task))
                    break;

                // Setup the current group index
                threadContext.GroupIndex = Stride3D.DenseXY.ReconstructFromElementIndex(
                    threadIdx,
                    task.GroupDim);

                // Wait for all threads of all multiprocessors to arrive here
                Thread.MemoryBarrier();
                processorBarrier.SignalAndWait();

                try
                {
                    // If we are an active group thread
                    int groupSize = task.GroupDim.Size;
                    if (threadIdx < groupSize)
                    {
                        try
                        {
                            var launcher = task.KernelExecutionDelegate;

                            // Split the grid into different chunks that will be processed
                            // by the available multiprocessors
                            int linearGridDim = task.GridDim.Size;
                            int gridChunkSize = IntrinsicMath.DivRoundUp(
                                linearGridDim,
                                Accelerator.NumMultiprocessors);
                            int gridOffset = gridChunkSize * ProcessorIndex;
                            int linearUserDim = task.TotalUserDim.Size;
                            for (
                                int i = gridOffset, e = gridOffset + gridChunkSize;
                                i < e;
                                ++i)
                            {
                                BeginThreadProcessing();
                                try
                                {
                                    // Setup the current grid index
                                    threadContext.GridIndex = Stride3D.DenseXY
                                        .ReconstructFromElementIndex(
                                        i,
                                        task.GridDim);

                                    // Invoke the actual kernel launcher
                                    int globalIndex = i * groupSize + threadIdx;
                                    if (globalIndex < linearUserDim)
                                        launcher(task, globalIndex);
                                }
                                finally
                                {
                                    EndThreadProcessing();
                                }
                            }
                        }
                        finally
                        {
                            // This thread has already finished processing
                            FinishThreadProcessing();
                        }
                    }
                }
                finally
                {
                    // Wait for all threads of all multiprocessors to arrive here
                    processorBarrier.SignalAndWait();

                    // If we reach this point and we are the main thread, notify the
                    // parent accelerator instance
                    if (isMainThread)
                        Accelerator.FinishTaskProcessing();
                }
            }
        }

        #endregion

        #region Internal Execution Methods

        /// <summary>
        /// Begins a accelerator task.
        /// </summary>
        /// <param name="task">The task to launch.</param>
        protected abstract void BeginLaunch(CPUAcceleratorTask task);

        /// <summary>
        /// Begins processing of the current thread.
        /// </summary>
        protected abstract void BeginThreadProcessing();

        /// <summary>
        /// Ends a previously started processing task of the current thread.
        /// </summary>
        protected abstract void EndThreadProcessing();

        /// <summary>
        /// Finishes processing of the current thread.
        /// </summary>
        protected abstract void FinishThreadProcessing();

        /// <summary>
        /// Waits for all threads in the current warp.
        /// </summary>
        /// <returns>The number of participating threads.</returns>
        public abstract int WarpBarrier();

        /// <summary>
        /// Waits for all threads in the current group.
        /// </summary>
        /// <returns>The number of participating threads.</returns>
        public abstract int GroupBarrier();

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var thread in threads)
                {
                    if ((thread.ThreadState & ThreadState.Unstarted) !=
                        ThreadState.Unstarted)
                    {
                        thread.Join();
                    }
                }
                groupContext.Dispose();
                foreach (var warp in warpContexts)
                    warp.Dispose();
                processorBarrier.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    partial class CPUMultiprocessor
    {
        #region Nested Types

        /// <summary>
        /// A sequential multiprocessor.
        /// </summary>
        private sealed class SequentialProcessor : CPUMultiprocessor
        {
            #region Instance

            /// <summary>
            /// The internal activity set.
            /// </summary>
            private volatile BitArray activitySet = new BitArray(1024);

            /// <summary>
            /// The internal index of the currently active thread.
            /// </summary>
            private volatile int activeThreadIndex;

            /// <summary>
            /// Creates a new sequential processor.
            /// </summary>
            public SequentialProcessor(CPUAccelerator accelerator, int processorIndex)
                : base(accelerator, processorIndex)
            { }

            #endregion

            #region Helper Methods

            /// <summary>
            /// Puts the current thread into sleep mode (if there are some other threads
            /// being active) and wakes up the next thread.
            /// </summary>
            /// <param name="threadOffset">The absolute thread offset.</param>
            /// <param name="threadIndex">The current thread index.</param>
            /// <param name="threadDimension">The current number of threads.</param>
            private void ScheduleNextThread(
                int threadOffset,
                int threadIndex,
                int threadDimension)
            {
                // Commit memory operations
                Thread.MemoryBarrier();

                // Determine the next thread that might become active
                lock (activitySet.SyncRoot)
                {
                    // Determine the next thread that might become active
                    for (int i = 1; i < threadDimension; ++i)
                    {
                        // Compute absolute thread index
                        int index = threadOffset + ((threadIndex + i) % threadDimension);
                        if (activitySet[index])
                        {
                            // This thread can become active
                            activeThreadIndex = index;
                            Monitor.PulseAll(activitySet.SyncRoot);
                            break;
                        }
                    }
                }

                // Commit memory operations
                Thread.MemoryBarrier();
            }

            /// <summary>
            /// Waits for the current thread to become active.
            /// </summary>
            /// <param name="threadOffset">The absolute thread offset.</param>
            /// <param name="threadIndex">The current thread index.</param>
            /// <returns>The number of participating threads.</returns>
            private int WaitForThreadToBecomeActive(int threadOffset, int threadIndex)
            {
                // Adjust relative thread index
                threadIndex += threadOffset;

                // NOTE: the current thread lock cannot be null
                lock (activitySet.SyncRoot)
                {
                    // Wait for our thread to become active
                    while (activeThreadIndex != threadIndex)
                        Monitor.Wait(activitySet.SyncRoot);

                    // We have become active... continue processing
                    return activitySet.Count;
                }
            }

            #endregion

            #region Runtime Methods

            /// <summary>
            /// Initializes the internal activity set.
            /// </summary>
            protected override void BeginLaunch(CPUAcceleratorTask task)
            {
                int groupSize = task.GroupDim.Size;
                if (activitySet.Length < groupSize)
                    activitySet = new BitArray(groupSize);

                // Mark all threads as active and activate the first one
                activitySet.SetAll(true);
                activeThreadIndex = 0;
            }

            /// <summary>
            /// Waits for the next thread to become active.
            /// </summary>
            protected override void BeginThreadProcessing() =>
                WaitForThreadToBecomeActive(
                    0,
                    CPURuntimeThreadContext.Current.LinearGroupIndex);

            /// <summary>
            /// Schedules the next thread in the waiting list.
            /// </summary>
            protected override void EndThreadProcessing() =>
                ScheduleNextThread(
                    0,
                    CPURuntimeThreadContext.Current.LinearGroupIndex,
                    groupContext.GroupSize);

            /// <summary>
            /// Removes the current thread from the activity set.
            /// </summary>
            protected override void FinishThreadProcessing()
            {
                // Remove the current thread from the set
                lock (activitySet.SyncRoot)
                {
                    activitySet.Set(
                        CPURuntimeThreadContext.Current.LinearGroupIndex,
                        false);
                }
            }

            /// <summary>
            /// Schedules the next thread to become active while waiting for this
            /// thread to become active again.
            /// </summary>
            /// <returns>The number of participating threads.</returns>
            public override int WarpBarrier()
            {
                // Get warp thread index
                var currentContext = CPURuntimeThreadContext.Current;
                int threadOffset = currentContext.WarpIndex * WarpSize;
                int threadIndex = currentContext.LaneIndex;

                // We have hit an inter-group thread barrier that requires all other
                // threads to run until this point
                ScheduleNextThread(threadOffset, threadIndex, WarpSize);

                // Wait for this thread to become active
                return WaitForThreadToBecomeActive(threadOffset, threadIndex);
            }

            /// <summary>
            /// Schedules the next thread to become active while waiting for this
            /// thread to become active again.
            /// </summary>
            /// <returns>The number of participating threads.</returns>
            public override int GroupBarrier()
            {
                // Get group thread index
                int threadIndex = CPURuntimeThreadContext.Current.LinearGroupIndex;

                // We have hit an inter-group thread barrier that requires all other
                // threads to run until this point
                ScheduleNextThread(0, threadIndex, groupContext.GroupSize);

                // Wait for this thread to become active
                return WaitForThreadToBecomeActive(0, threadIndex);
            }

            #endregion
        }

        /// <summary>
        /// A parallel multiprocessor.
        /// </summary>
        private sealed class ParallelProcessor : CPUMultiprocessor
        {
            #region Static

            /// <summary>
            /// Initializes a given barrier to ensure that the barrier has a sufficient
            /// number of participants.
            /// </summary>
            /// <param name="barrier">The barrier to initialize.</param>
            /// <param name="numParticipants">The number of desired participants.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void InitBarrier(Barrier barrier, int numParticipants)
            {
                int currentBarrierCount = barrier.ParticipantCount;
                if (currentBarrierCount > numParticipants)
                    barrier.RemoveParticipants(currentBarrierCount - numParticipants);
                else if (currentBarrierCount < numParticipants)
                    barrier.AddParticipants(numParticipants - currentBarrierCount);
            }

            /// <summary>
            /// Invokes <see cref="Barrier.SignalAndWait()"/> on the given barrier while
            /// ensuring that all memory transactions have been committed. Furthermore,
            /// it determines the number of participating threads.
            /// </summary>
            /// <param name="barrier">The barrier to use.</param>
            /// <returns>The number of participating threads.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int PerformBarrier(Barrier barrier)
            {
                // Issue a thread memory barrier first to ensure that no IO operations
                // will be reorder across this barrier
                Thread.MemoryBarrier();

                // Determine the number of participants which can be smaller than the
                // current thread dimension. Note that it is safe to query the number of
                // participants at this point since no other RemoveParticipant operation
                // can be executed in parallel by construction.
                int numParticipants = barrier.ParticipantCount;

                // Wait for all other participants
                barrier.SignalAndWait();

                return numParticipants;
            }

            #endregion

            #region Instance

            /// <summary>
            /// The general barrier.
            /// </summary>
            private readonly Barrier barrier = new Barrier(0);

            /// <summary>
            /// Warp barriers for each warp.
            /// </summary>
            private readonly Barrier[] warpBarriers;

            /// <summary>
            /// Creates a new parallel processor.
            /// </summary>
            public ParallelProcessor(CPUAccelerator accelerator, int processorIndex)
                : base(accelerator, processorIndex)
            {
                // Initialize all warp barriers
                warpBarriers = new Barrier[NumWarpsPerMultiprocessor];
                for (int i = 0, e = NumWarpsPerMultiprocessor; i < e; ++i)
                    warpBarriers[i] = new Barrier(0);
            }

            #endregion

            #region Runtime Methods

            /// <summary>
            /// Ensures that the internal barriers are properly initialized.
            /// </summary>
            protected override void BeginLaunch(CPUAcceleratorTask task)
            {
                InitBarrier(barrier, task.GroupDim.Size);
                for (int i = 0, e = NumWarpsPerMultiprocessor; i < e; ++i)
                {
                    int currentWarpSize = warpContexts[i].CurrentWarpSize;
                    InitBarrier(warpBarriers[i], currentWarpSize);
                }
            }

            /// <summary>
            /// Performs no operation.
            /// </summary>
            protected override void BeginThreadProcessing() { }

            /// <summary>
            /// Waits for the internal barrier.
            /// </summary>
            protected override void EndThreadProcessing() => barrier.SignalAndWait();

            /// <summary>
            /// Removes a participant from the internal thread barrier.
            /// </summary>
            protected override void FinishThreadProcessing() =>
                barrier.RemoveParticipant();

            /// <summary>
            /// Waits for all threads of the current warp.
            /// </summary>
            public override int WarpBarrier()
            {
                // Determine the actual warp and perform the barrier operation
                int warpIndex = CPURuntimeThreadContext.Current.WarpIndex;
                return PerformBarrier(warpBarriers[warpIndex]);
            }

            /// <summary>
            /// Waits for all threads using the underlying thread barrier.
            /// </summary>
            public override int GroupBarrier() => PerformBarrier(barrier);

            #endregion

            #region IDisposable

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    barrier.Dispose();
                    foreach (var warpBarrier in warpBarriers)
                        warpBarrier.Dispose();
                }
                base.Dispose(disposing);
            }

            #endregion
        }

        #endregion
    }
}
