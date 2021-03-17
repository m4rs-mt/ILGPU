// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPUAccelerator.Execution.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ThreadState = System.Threading.ThreadState;

namespace ILGPU.Runtime.CPU
{
    partial class CPUAccelerator
    {
        #region Instance

        // Execution threads

        private Thread[] threads;
        private Barrier[] processorBarriers;
        private CPURuntimeGroupContext[] groupContexts;
        private CPURuntimeWarpContext[,] warpContexts;

        // General execution management

        private volatile bool running = true;
        private volatile int maxNumLaunchedThreadsPerGroup;

        // Task execution

        private readonly object taskSynchronizationObject = new object();
        private volatile CPUAcceleratorTask currentTask;
        private Barrier finishedEventPerMultiprocessor;

        /// <summary>
        /// Initializes the internal execution engine.
        /// </summary>
        /// <param name="threadPriority">The desired thread priority.</param>
        private void InitExecutionEngine(ThreadPriority threadPriority)
        {
            threads = new Thread[NumThreads];
            finishedEventPerMultiprocessor = new Barrier(NumMultiprocessors + 1);

            // Setup all warp and group contexts
            int numWarpsPerMultiprocessor = MaxNumThreadsPerMultiprocessor / WarpSize;
            warpContexts = new CPURuntimeWarpContext[
                NumMultiprocessors,
                numWarpsPerMultiprocessor];
            groupContexts = new CPURuntimeGroupContext[NumMultiprocessors];
            processorBarriers = new Barrier[NumMultiprocessors];
            for (int i = 0; i < NumMultiprocessors; ++i)
            {
                for (int j = 0; j < numWarpsPerMultiprocessor; ++j)
                {
                    warpContexts[i, j] = new CPURuntimeWarpContext(
                        this,
                        WarpSize);
                }

                groupContexts[i] = UsesSequentialExecution
                    ? new SequentialCPURuntimeGroupContext(this) as CPURuntimeGroupContext
                    : new ParallelCPURuntimeGroupContext(this);
                processorBarriers[i] = new Barrier(0);
            }

            // Instantiate all runtime threads
            Parallel.For(0, NumThreads, i =>
            {
                var thread = threads[i] = new Thread(ExecuteThread)
                {
                    IsBackground = true,
                    Priority = threadPriority,
                };
                thread.Name = $"ILGPU_{InstanceId}_CPU_{i}";
            });

            // Start or delay the creation of runtime threads
            if (NumThreads <= 32)
                StartOrContinueRuntimeThreads(MaxNumThreadsPerMultiprocessor);
            else
                maxNumLaunchedThreadsPerGroup = 0;
        }

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

            // Launch all threads that we need for processing
            for (int i = 0; i < NumMultiprocessors; ++i)
            {
                Parallel.For(maxNumLaunchedThreadsPerGroup, groupSize, threadIdx =>
                {
                    int globalThreadIdx = i * MaxNumThreadsPerMultiprocessor + threadIdx;
                    threads[globalThreadIdx].Start(globalThreadIdx);
                });
            }
            maxNumLaunchedThreadsPerGroup = groupSize;

            // Adjust number of threads per MP
            for (int i = 0; i < NumMultiprocessors; ++i)
            {
                var processorBarrier = processorBarriers[i];
                if (processorBarrier.ParticipantCount < maxNumLaunchedThreadsPerGroup)
                {
                    processorBarrier.AddParticipants(
                        maxNumLaunchedThreadsPerGroup -
                        processorBarrier.ParticipantCount);
                }
            }
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

            for (int i = 0, e = NumMultiprocessors; i < e; ++i)
            {
                // Initialize the associated group context
                var context = groupContexts[i];
                context.Initialize(
                    task.GridDim,
                    task.GroupDim,
                    task.DynamicSharedMemoryConfig);

                // Initialize each involved warp context
                for (int j = 0, e2 = numWarps - 1; j < e2; ++j)
                    warpContexts[i, j].Initialize(WarpSize);

                int lastWarpSize = groupSize % WarpSize == 0
                    ? WarpSize
                    : groupSize % WarpSize;
                warpContexts[i, numWarps - 1].Initialize(lastWarpSize);
            }

            // Setup sequential execution objects
        }

        /// <summary>
        /// Launches the given accelerator task on this accelerator.
        /// </summary>
        /// <param name="task">The task to launch.</param>
        internal void Launch(CPUAcceleratorTask task)
        {
            Debug.Assert(task != null, "Invalid accelerator task");

            SetupRuntimeClasses(task);
            StartOrContinueRuntimeThreads(task.GroupDim.Size);
            Interlocked.MemoryBarrier();

            // Launch all processing threads
            lock (taskSynchronizationObject)
            {
                Debug.Assert(currentTask == null, "Invalid concurrent modification");
                currentTask = task;
                Monitor.PulseAll(taskSynchronizationObject);
            }

            // Wait for the result
            finishedEventPerMultiprocessor.SignalAndWait();

            // Reset all groups
            for (int i = 0, e = NumMultiprocessors; i < e; ++i)
                groupContexts[i].TearDown();

            // Reset task
            lock (taskSynchronizationObject)
                currentTask = null;
        }

        /// <summary>
        /// Entry point for a single processing thread.
        /// </summary>
        /// <param name="arg">The absolute thread index.</param>
        private void ExecuteThread(object arg)
        {
            // Get the current thread information
            int absoluteThreadIndex = (int)arg;
            int threadIdx = absoluteThreadIndex % MaxNumThreadsPerMultiprocessor;
            var processorIdx = absoluteThreadIndex / MaxNumThreadsPerMultiprocessor;

            var processorBarrier = processorBarriers[processorIdx];
            bool isMainThread = threadIdx == 0;

            // Setup a new thread context for this thread and initialize the lane index
            int laneIdx = threadIdx % WarpSize;
            var threadContext = new CPURuntimeThreadContext(laneIdx)
            {
                LinearGroupIndex = threadIdx
            };
            threadContext.MakeCurrent();

            // Setup the current warp context as it always stays the same
            int warpIdx = threadIdx / WarpSize;
            bool isMainWarpThread = threadIdx == 0;
            var warpContext = warpContexts[processorIdx, warpIdx];
            warpContext.MakeCurrent();

            // Setup the current group context as it always stays the same
            var groupContext = groupContexts[processorIdx];
            groupContext.MakeCurrent();

            CPUAcceleratorTask task = null;
            for (; ; )
            {
                // Get a new task to execute
                lock (taskSynchronizationObject)
                {
                    while ((currentTask == null | currentTask == task) & running)
                        Monitor.Wait(taskSynchronizationObject);
                    if (!running)
                        break;
                    task = currentTask;
                }
                Debug.Assert(task != null, "Invalid task");

                // Setup the current group index
                threadContext.GroupIndex = Index3.ReconstructIndex(
                    threadIdx,
                    task.GroupDim);

                // Wait for all threads of all multiprocessors to arrive here
                Thread.MemoryBarrier();
                processorBarrier.SignalAndWait();

                // If we are an active group thread
                int groupSize = task.GroupDim.Size;
                if (threadIdx < groupSize)
                {
                    var launcher = task.KernelExecutionDelegate;

                    // Split the grid into different chunks that will be processed by the
                    // available multiprocessors
                    int linearGridDim = task.GridDim.Size;
                    int gridChunkSize = IntrinsicMath.DivRoundUp(
                        linearGridDim,
                        NumMultiprocessors);
                    int gridOffset = gridChunkSize * processorIdx;
                    int linearUserDim = task.TotalUserDim.Size;
                    for (int i = gridOffset, e = gridOffset + gridChunkSize; i < e; ++i)
                    {
                        groupContext.BeginThreadProcessing();

                        // Setup the current grid index
                        threadContext.GridIndex = Index3.ReconstructIndex(
                            i,
                            task.GridDim);

                        // Invoke the actual kernel launcher
                        int globalIndex = i * groupSize + threadIdx;
                        if (globalIndex < linearUserDim)
                            launcher(task, globalIndex);

                        groupContext.EndThreadProcessing();
                    }

                    // This thread has already finished processing
                    groupContext.FinishThreadProcessing();
                    warpContext.FinishThreadProcessing();
                }

                // Wait for all threads of all multiprocessors to arrive here
                processorBarrier.SignalAndWait();

                // If we reach this point and we are the main thread, notify the parent
                // accelerator instance
                if (isMainThread)
                    finishedEventPerMultiprocessor.SignalAndWait();
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes all parts of the execution engine.
        /// </summary>
        private void DisposeExecutionEngine(bool disposing)
        {
            if (!disposing)
                return;

            // Dispose task engine
            lock (taskSynchronizationObject)
            {
                running = false;
                currentTask = null;
                Monitor.PulseAll(taskSynchronizationObject);
            }
            foreach (var thread in threads)
            {
                if ((thread.ThreadState & ThreadState.Unstarted) !=
                    ThreadState.Unstarted)
                {
                    thread.Join();
                }
            }
            foreach (var group in groupContexts)
                group.Dispose();
            foreach (var warp in warpContexts)
                warp.Dispose();

            // Dispose barriers
            foreach (var barrier in processorBarriers)
                barrier.Dispose();
            finishedEventPerMultiprocessor.Dispose();
        }

        #endregion
    }
}
