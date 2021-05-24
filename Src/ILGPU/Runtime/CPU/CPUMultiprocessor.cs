// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPUMultiprocessor.cs
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
using System.Threading.Tasks;
using ThreadState = System.Threading.ThreadState;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents an abstract GPU multiprocessor simulated on the CPU.
    /// </summary>
    abstract partial class CPUMultiprocessor : DisposeBase
    {
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

            // Launch all threads that we need for processing
            Parallel.For(maxNumLaunchedThreadsPerGroup, groupSize, threadIdx =>
            {
                int globalThreadIdx = ProcessorIndex * MaxNumThreadsPerMultiprocessor
                    + threadIdx;
                threads[globalThreadIdx].Start(globalThreadIdx);
            });
            maxNumLaunchedThreadsPerGroup = groupSize;

            // Adjust number of threads per MP
            if (processorBarrier.ParticipantCount < maxNumLaunchedThreadsPerGroup)
            {
                processorBarrier.AddParticipants(
                    maxNumLaunchedThreadsPerGroup -
                    processorBarrier.ParticipantCount);
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
            bool isMainWarpThread = threadIdx == 0;
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
                threadContext.GroupIndex = Index3D.ReconstructIndex(
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
                                    threadContext.GridIndex = Index3D.ReconstructIndex(
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
}
