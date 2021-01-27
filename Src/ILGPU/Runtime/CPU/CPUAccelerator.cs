// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPUAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.IL;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a general CPU-based runtime for kernels.
    /// </summary>
    public sealed class CPUAccelerator : Accelerator
    {
        #region Static

        /// <summary>
        /// Represents the main CPU accelerator.
        /// </summary>
        public static CPUAcceleratorId CPUAcceleratorId => CPUAcceleratorId.Instance;

        /// <summary>
        /// Represents all available CPU accelerators.
        /// </summary>
        public static ImmutableArray<CPUAcceleratorId> CPUAccelerators { get; } =
            ImmutableArray.Create(CPUAcceleratorId);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an NVIDIA
        /// GPU having 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateNvidiaSimulator(Context context) =>
            new CPUAccelerator(context, 32, 32, 1);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an AMD
        /// GPU having 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateAMDSimulator(Context context) =>
            new CPUAccelerator(context, 32, 8, 1);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of a legacy
        /// GCN AMD GPU having 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateLegacyAMDSimulator(Context context) =>
            new CPUAccelerator(context, 64, 4, 1);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an Intel
        /// GPU having 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateIntelSimulator(Context context) =>
            new CPUAccelerator(context, 16, 8, 1);

        #endregion

        #region Instance

        private readonly Thread[] threads;
        private readonly Barrier[] processorBarriers;
        private readonly CPURuntimeGroupContext[] groupContexts;
        private readonly CPURuntimeWarpContext[,] warpContexts;

        private readonly object taskSynchronizationObject = new object();
        private volatile CPUAcceleratorTask currentTask;
        private volatile bool running = true;
        private readonly Barrier finishedEventPerMultiprocessor;

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        public CPUAccelerator(Context context)
            : this(context, Environment.ProcessorCount * 2)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">
        /// The number of threads for parallel processing.
        /// </param>
        public CPUAccelerator(Context context, int numThreads)
            : this(context, numThreads, ThreadPriority.Normal)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">
        /// The number of threads for parallel processing.
        /// </param>
        /// <param name="threadPriority">
        /// The thread priority of the execution threads.
        /// </param>
        public CPUAccelerator(
            Context context,
            int numThreads,
            ThreadPriority threadPriority)
            : this(
                  context,
                  Math.Max(numThreads, 2),
                  Math.Min(Math.Max(numThreads, 2), 32),
                  1,
                  threadPriority)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreadsPerWarp">
        /// The number of threads per warp within a group.
        /// </param>
        /// <param name="numWarpsPerMultiprocessor">
        /// The number of warps per multiprocessor.
        /// </param>
        /// <param name="numMultiprocessors">
        /// The number of multiprocessors (number of parallel groups) to simulate.
        /// </param>
        public CPUAccelerator(
            Context context,
            int numThreadsPerWarp,
            int numWarpsPerMultiprocessor,
            int numMultiprocessors)
            : this(
                  context,
                  numThreadsPerWarp,
                  numWarpsPerMultiprocessor,
                  numMultiprocessors,
                  ThreadPriority.Normal)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreadsPerWarp">
        /// The number of threads per group for parallel processing.
        /// </param>
        /// <param name="numWarpsPerMultiprocessor">
        /// The number of warps per multiprocessor.
        /// </param>
        /// <param name="numMultiprocessors">
        /// The number of multiprocessors (number of parallel groups) to simulate.
        /// </param>
        /// <param name="threadPriority">
        /// The thread priority of the execution threads.
        /// </param>
        public CPUAccelerator(
            Context context,
            int numThreadsPerWarp,
            int numWarpsPerMultiprocessor,
            int numMultiprocessors,
            ThreadPriority threadPriority)
            : base(context, AcceleratorType.CPU)
        {
            if (numThreadsPerWarp < 2 || !Utilities.IsPowerOf2(numWarpsPerMultiprocessor))
                throw new ArgumentOutOfRangeException(nameof(numThreadsPerWarp));
            if (numWarpsPerMultiprocessor < 1)
                throw new ArgumentOutOfRangeException(nameof(numWarpsPerMultiprocessor));
            if (numMultiprocessors < 1)
                throw new ArgumentOutOfRangeException(nameof(numMultiprocessors));

            // Check for existing limitations with respect to barrier participants
            if (numThreadsPerWarp * numWarpsPerMultiprocessor > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(numWarpsPerMultiprocessor));
            if (NumMultiprocessors > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(numMultiprocessors));

            NativePtr = new IntPtr(1);
            WarpSize = numThreadsPerWarp;
            MaxNumThreadsPerGroup = numThreadsPerWarp * numWarpsPerMultiprocessor;
            MaxNumThreadsPerMultiprocessor = MaxNumThreadsPerGroup;
            NumMultiprocessors = numMultiprocessors;
            NumThreads = MaxNumThreads * numMultiprocessors;

            threads = new Thread[NumThreads];
            finishedEventPerMultiprocessor = new Barrier(numMultiprocessors + 1);

            // Setup all warp and group contexts
            warpContexts = new CPURuntimeWarpContext[
                numMultiprocessors,
                numWarpsPerMultiprocessor];
            groupContexts = new CPURuntimeGroupContext[numMultiprocessors];
            processorBarriers = new Barrier[numMultiprocessors];
            for (int i = 0; i < numMultiprocessors; ++i)
            {
                for (int j = 0; j < numWarpsPerMultiprocessor; ++j)
                {
                    warpContexts[i, j] = new CPURuntimeWarpContext(
                        this,
                        numThreadsPerWarp);
                }

                groupContexts[i] = new CPURuntimeGroupContext(this);
                processorBarriers[i] = new Barrier(MaxNumThreadsPerMultiprocessor);
            }

            // Instantiate all runtime threads
            for (int i = 0; i < NumThreads; ++i)
            {
                var thread = threads[i] = new Thread(ExecuteThread)
                {
                    IsBackground = true,
                    Priority = threadPriority,
                };
                thread.Name = $"ILGPU_{InstanceId}_CPU_{i}";
                thread.Start(i);
            }

            DefaultStream = CreateStream();
            Name = nameof(CPUAccelerator);
            MemorySize = long.MaxValue;
            MaxGridSize = new Index3(int.MaxValue, int.MaxValue, int.MaxValue);
            MaxNumThreadsPerGroup = NumThreads;
            MaxGroupSize = new Index3(
                MaxNumThreadsPerGroup,
                MaxNumThreadsPerGroup,
                MaxNumThreadsPerGroup);
            MaxSharedMemoryPerGroup = int.MaxValue;
            MaxConstantMemory = int.MaxValue;

            Bind();
            Init(context.DefautltILBackend);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of threads.
        /// </summary>
        public int NumThreads { get; }

        #endregion

        #region Methods

        /// <summary cref="Accelerator.CreateExtension{TExtension, TExtensionProvider}
        /// (TExtensionProvider)"/>
        public override TExtension CreateExtension<
            TExtension,
            TExtensionProvider>(TExtensionProvider provider) =>
            provider.CreateCPUExtension(this);

        /// <summary cref="Accelerator.AllocateInternal{T, TIndex}(TIndex)"/>
        protected override MemoryBuffer<T, TIndex> AllocateInternal<
            T,
            TIndex>(TIndex extent) =>
            new CPUMemoryBuffer<T, TIndex>(this, extent);

        /// <summary>
        /// Loads the given kernel.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="customGroupSize">The custom group size.</param>
        /// <returns>The loaded kernel</returns>
        private Kernel LoadKernel(CompiledKernel kernel, int customGroupSize)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (!(kernel is ILCompiledKernel ilKernel))
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedKernel);
            }

            var launcherMethod = GenerateKernelLauncherMethod(
                ilKernel,
                customGroupSize);
            return new CPUKernel(
                this,
                kernel,
                launcherMethod,
                ilKernel.ExecutionHandler);
        }

        /// <summary>
        /// Loads a default kernel.
        /// </summary>
        protected override Kernel LoadKernelInternal(CompiledKernel kernel) =>
            LoadKernel(kernel, 0);

        /// <summary>
        /// Loads an implicitly grouped kernel.
        /// </summary>
        protected override Kernel LoadImplicitlyGroupedKernelInternal(
            CompiledKernel kernel,
            int customGroupSize,
            out KernelInfo kernelInfo)
        {
            if (customGroupSize < 0)
                throw new ArgumentOutOfRangeException(nameof(customGroupSize));
            kernelInfo = KernelInfo.CreateFrom(
                kernel.Info,
                customGroupSize,
                null);
            return LoadKernel(kernel, customGroupSize);
        }

        /// <summary>
        /// Loads an auto grouped kernel.
        /// </summary>
        protected override Kernel LoadAutoGroupedKernelInternal(
            CompiledKernel kernel,
            out KernelInfo kernelInfo)
        {
            var result = LoadKernel(kernel, WarpSize);
            kernelInfo = new KernelInfo(WarpSize, NumThreads / WarpSize);
            return result;
        }

        /// <summary cref="Accelerator.CreateStreamInternal()"/>
        protected override AcceleratorStream CreateStreamInternal() =>
            new CPUStream(this);

        /// <summary cref="Accelerator.Synchronize"/>
        protected override void SynchronizeInternal() { }

        /// <summary cref="Accelerator.OnBind"/>
        protected override void OnBind() { }

        /// <summary cref="Accelerator.OnUnbind"/>
        protected override void OnUnbind() { }

        #endregion

        #region Peer Access

        /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
        protected override bool CanAccessPeerInternal(Accelerator otherAccelerator) =>
            otherAccelerator as CPUAccelerator != null;

        /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
        protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
        {
            if (otherAccelerator as CPUAccelerator == null)
            {
                throw new InvalidOperationException(
                    RuntimeErrorMessages.CannotEnablePeerAccessToOtherAccelerator);
            }
        }

        /// <summary cref="Accelerator.DisablePeerAccess(Accelerator)"/>
        protected override void DisablePeerAccessInternal(
            Accelerator otherAccelerator) =>
            Debug.Assert(
                otherAccelerator is CPUAccelerator,
                "Invalid EnablePeerAccess method");

        #endregion

        #region Launch Methods

        /// <summary>
        /// Launches the given accelerator task on this accelerator.
        /// </summary>
        /// <param name="task">The task to launch.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Launch(CPUAcceleratorTask task)
        {
            Debug.Assert(task != null, "Invalid accelerator task");

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
        /// <param name="arg">The relative thread index.</param>
        private void ExecuteThread(object arg)
        {
            // Compute the thread indices
            int absoluteThreadIdx = (int)arg;
            int threadIdx = absoluteThreadIdx % MaxNumThreadsPerMultiprocessor;
            bool isMainThread = threadIdx == 0;

            // Get processor related information
            int processorIdx = absoluteThreadIdx / MaxNumThreadsPerMultiprocessor;
            var processorBarrier = processorBarriers[processorIdx];

            // Setup a new thread context for this thread and initialize the lane idx
            int laneIdx = threadIdx % WarpSize;
            var threadContext = new CPURuntimeThreadContext(laneIdx);
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
                        // Setup the current grid index
                        threadContext.GridIndex = Index3.ReconstructIndex(
                            i,
                            task.GridDim);

                        // Invoke the actual kernel launcher
                        int globalIndex = i * groupSize + threadIdx;
                        if (globalIndex < linearUserDim)
                            launcher(task, globalIndex);

                        // Wait for all group threads to arrive
                        groupContext.WaitForNextThreadIndex();
                    }

                    // This thread has already finished processing
                    warpContext.OnKernelExecutionCompleted();
                    groupContext.OnKernelExecutionCompleted();
                }

                // Wait for all threads of all multiprocessors to arrive here
                processorBarrier.SignalAndWait();

                // If we reach this point and we are the main thread, notify the parent
                // accelerator instance
                if (isMainThread)
                    finishedEventPerMultiprocessor.SignalAndWait();
            }
        }

        /// <summary>
        /// Generates a dynamic kernel-launcher method that will be just-in-time compiled
        /// during the first invocation. Using the generated launcher lowers the overhead
        /// for kernel launching dramatically, since unnecessary operations (like boxing)
        /// can be avoided.
        /// </summary>
        /// <param name="kernel">The kernel to generate a launcher for.</param>
        /// <param name="customGroupSize">
        /// The custom group size for the launching operation.
        /// </param>
        /// <returns>The generated launcher method.</returns>
        private MethodInfo GenerateKernelLauncherMethod(
            ILCompiledKernel kernel,
            int customGroupSize)
        {
            var entryPoint = kernel.EntryPoint;
            AdjustAndVerifyKernelGroupSize(ref customGroupSize, entryPoint);

            using var scopedLock = entryPoint.CreateLauncherMethod(
                Context.RuntimeSystem,
                out var launcher);
            var emitter = new ILEmitter(launcher.ILGenerator);

            var cpuKernel = emitter.DeclareLocal(typeof(CPUKernel));
            KernelLauncherBuilder.EmitLoadKernelArgument<CPUKernel, ILEmitter>(
                Kernel.KernelInstanceParamIdx, emitter);
            emitter.Emit(LocalOperation.Store, cpuKernel);

            // Create an instance of the custom task type
            var task = emitter.DeclareLocal(kernel.TaskType);
            {
                emitter.Emit(LocalOperation.Load, cpuKernel);
                emitter.EmitCall(CPUKernel.GetKernelExecutionDelegate);

                // Load custom user dimension
                KernelLauncherBuilder.EmitLoadKernelConfig(
                    entryPoint,
                    emitter,
                    Kernel.KernelParamDimensionIdx);

                // Load dimensions
                KernelLauncherBuilder.EmitLoadRuntimeKernelConfig(
                    entryPoint,
                    emitter,
                    Kernel.KernelParamDimensionIdx,
                    customGroupSize);

                // Create new task object
                emitter.EmitNewObject(kernel.TaskConstructor);

                // Store task
                emitter.Emit(LocalOperation.Store, task);
            }

            // Assign parameters
            var parameters = entryPoint.Parameters;
            for (int i = 0, e = parameters.Count; i < e; ++i)
            {
                emitter.Emit(LocalOperation.Load, task);
                emitter.Emit(ArgumentOperation.Load, i + Kernel.KernelParameterOffset);
                if (parameters.IsByRef(i))
                    emitter.Emit(OpCodes.Ldobj, parameters[i]);
                emitter.Emit(OpCodes.Stfld, kernel.TaskArgumentMapping[i]);
            }

            // Launch task: ((CPUKernel)kernel).CPUAccelerator.Launch(task);
            emitter.Emit(LocalOperation.Load, cpuKernel);
            emitter.EmitCall(
                typeof(CPUKernel).GetProperty(
                    nameof(CPUKernel.CPUAccelerator)).GetGetMethod(false));
            emitter.Emit(LocalOperation.Load, task);
            emitter.EmitCall(
                typeof(CPUAccelerator).GetMethod(
                    nameof(CPUAccelerator.Launch),
                    BindingFlags.NonPublic | BindingFlags.Instance));

            // End of launch method
            emitter.Emit(OpCodes.Ret);
            emitter.Finish();

            return launcher.Finish();
        }

        #endregion

        #region Occupancy

        /// <summary cref="Accelerator.EstimateMaxActiveGroupsPerMultiprocessor(
        /// Kernel, int, int)"/>
        protected override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes) =>
            kernel is CPUKernel
            ? NumThreads / groupSize
            : throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(
        /// Kernel, Func{int, int}, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize)
        {
            if (!(kernel is CPUKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            // Estimation
            minGridSize = NumThreads;
            return 1;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(
        /// Kernel, int, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            if (!(kernel is CPUKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            // Estimation
            minGridSize = NumThreads;
            return 1;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose all managed resources allocated by this CPU accelerator instance.
        /// </summary>
        protected override void DisposeAccelerator_SyncRoot(bool disposing)
        {
            if (!disposing)
                return;

            // Dispose all managed objects
            lock (taskSynchronizationObject)
            {
                running = false;
                currentTask = null;
                Monitor.PulseAll(taskSynchronizationObject);
            }
            foreach (var thread in threads)
                thread.Join();
            threads = null;
            foreach (var group in groupContexts)
                group.Dispose();
            foreach (var warp in warpContexts)
                warp.Dispose();
            finishedEvent.Dispose();
        }

        #endregion
    }
}
