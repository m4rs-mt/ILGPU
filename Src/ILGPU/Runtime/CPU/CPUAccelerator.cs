// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: CPUAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

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
        public static AcceleratorId CPUAcceleratorId =>
            new AcceleratorId(AcceleratorType.CPU, 0);

        /// <summary>
        /// Represents all available CPU accelerators.
        /// </summary>
        public static ImmutableArray<AcceleratorId> CPUAccelerators { get; } =
            ImmutableArray.Create(CPUAcceleratorId);

        #endregion

        #region Instance

        private Thread[] threads;
        private CPURuntimeGroupContext[] groupContexts;

        private readonly object taskSynchronizationObject = new object();
        private volatile CPUAcceleratorTask currentTask;
        private volatile bool running = true;
        private readonly Barrier finishedEvent;

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        public CPUAccelerator(Context context)
            : this(context, Environment.ProcessorCount)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        public CPUAccelerator(Context context, int numThreads)
            : this(context, numThreads, ThreadPriority.Normal)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        public CPUAccelerator(
            Context context,
            int numThreads,
            ThreadPriority threadPriority)
            : base(context, AcceleratorType.CPU)
        {
            if (numThreads < 1)
                throw new ArgumentOutOfRangeException(nameof(numThreads));

            // Setup assembly and module builder for dynamic code generation

            NumThreads = numThreads;
            WarpSize = 1;
            threads = new Thread[numThreads];
            finishedEvent = new Barrier(numThreads + 1);
            // The maximum number of thread groups that can be handled in parallel is
            // equal to the number of available threads in the worst case.
            groupContexts = new CPURuntimeGroupContext[numThreads];
            for (int i = 0; i < numThreads; ++i)
            {
                groupContexts[i] = new CPURuntimeGroupContext(this);
                var thread = threads[i] = new Thread(ExecuteThread)
                {
                    IsBackground = true,
                    Priority = threadPriority,
                };
                thread.Name = "ILGPUExecutionThread" + i;
                thread.Start(i);
            }

            DefaultStream = CreateStream();
            Name = nameof(CPUAccelerator);
            MemorySize = long.MaxValue;
            MaxGridSize = new Index3(int.MaxValue, int.MaxValue, int.MaxValue);
            MaxNumThreadsPerGroup = NumThreads;
            MaxSharedMemoryPerGroup = int.MaxValue;
            MaxConstantMemory = int.MaxValue;
            NumMultiprocessors = 1;
            MaxNumThreadsPerMultiprocessor = NumThreads;
            Backend = context.DefautltILBackend;

            Bind();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of threads.
        /// </summary>
        public int NumThreads { get; }

        #endregion

        #region Methods

        /// <summary cref="Accelerator.CreateExtension{TExtension, TExtensionProvider}(TExtensionProvider)"/>
        public override TExtension CreateExtension<TExtension, TExtensionProvider>(TExtensionProvider provider)
        {
            return provider.CreateCPUExtension(this);
        }

        /// <summary cref="Accelerator.AllocateInternal{T, TIndex}(TIndex)"/>
        protected override MemoryBuffer<T, TIndex> AllocateInternal<T, TIndex>(TIndex extent)
        {
            return new CPUMemoryBuffer<T, TIndex>(this, extent);
        }

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
            var ilKernel = kernel as ILCompiledKernel;
            if (ilKernel == null)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            var launcherMethod = GenerateKernelLauncherMethod(ilKernel, customGroupSize);
            return new CPUKernel(
                this,
                kernel,
                launcherMethod,
                ilKernel.ExecutionHandler);
        }

        /// <summary cref="Accelerator.LoadKernelInternal(CompiledKernel)"/>
        protected override Kernel LoadKernelInternal(CompiledKernel kernel)
        {
            return LoadKernel(kernel, 0);
        }

        /// <summary cref="Accelerator.LoadImplicitlyGroupedKernelInternal(CompiledKernel, int)"/>
        protected override Kernel LoadImplicitlyGroupedKernelInternal(
            CompiledKernel kernel,
            int customGroupSize)
        {
            if (customGroupSize < 0)
                throw new ArgumentOutOfRangeException(nameof(customGroupSize));
            return LoadKernel(kernel, customGroupSize);
        }

        /// <summary cref="Accelerator.LoadAutoGroupedKernelInternal(CompiledKernel, out int, out int)"/>
        protected override Kernel LoadAutoGroupedKernelInternal(
            CompiledKernel kernel,
            out int groupSize,
            out int minGridSize)
        {
            groupSize = WarpSize;
            minGridSize = NumThreads / WarpSize;
            return LoadKernel(kernel, groupSize);
        }

        /// <summary cref="Accelerator.CreateStreamInternal"/>
        protected override AcceleratorStream CreateStreamInternal()
        {
            return new CPUStream(this);
        }

        /// <summary cref="Accelerator.Synchronize"/>
        protected override void SynchronizeInternal()
        { }

        /// <summary cref="Accelerator.OnBind"/>
        protected override void OnBind()
        { }

        /// <summary cref="Accelerator.OnUnbind"/>
        protected override void OnUnbind()
        { }

        #endregion

        #region Peer Access

        /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
        protected override bool CanAccessPeerInternal(Accelerator otherAccelerator)
        {
            return (otherAccelerator as CPUAccelerator) != null;
        }

        /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
        protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
        {
            if (otherAccelerator as CPUAccelerator == null)
                throw new InvalidOperationException(RuntimeErrorMessages.CannotEnablePeerAccessToDifferentAcceleratorKind);
        }

        /// <summary cref="Accelerator.DisablePeerAccess(Accelerator)"/>
        protected override void DisablePeerAccessInternal(Accelerator otherAccelerator)
        {
            Debug.Assert(otherAccelerator is CPUAccelerator, "Invalid EnablePeerAccess method");
        }

        #endregion

        #region Launch Methods

        /// <summary>
        /// Computes the number of required threads to reach the requested group size.
        /// </summary>
        /// <param name="groupSize">The requested group size.</param>
        /// <returns>The number of threads to reach the requested groupn size.</returns>
        private int ComputeNumGroupThreads(int groupSize)
        {
            var numThreads = groupSize + (groupSize % WarpSize);
            if (numThreads > NumThreads)
                throw new NotSupportedException($"Not supported total group size. The total group size must be <= the number of available threads ({NumThreads})");
            return numThreads;
        }

        /// <summary>
        /// Launches the given accelerator task on this accelerator.
        /// </summary>
        /// <param name="task">The task to launch.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Launch(CPUAcceleratorTask task)
        {
            Debug.Assert(task != null, "Invalid accelerator task");

            var groupThreadSize = ComputeNumGroupThreads(task.GroupDim.Size);

            // Setup groups
            var numRuntimeGroups = NumThreads / groupThreadSize;
            for (int i = 0; i < numRuntimeGroups; ++i)
            {
                var context = groupContexts[i];
                context.Initialize(task.GridDim, task.GroupDim, task.SharedMemSize);
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
            finishedEvent.SignalAndWait();

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
            var relativeThreadIdx = (int)arg;

            CPUAcceleratorTask task = null;
            for (;;)
            {
                lock (taskSynchronizationObject)
                {
                    while ((currentTask == null | currentTask == task) & running)
                        Monitor.Wait(taskSynchronizationObject);
                    if (!running)
                        break;
                    task = currentTask;
                }

                Debug.Assert(task != null, "Invalid task");

                var groupThreadSize = ComputeNumGroupThreads(task.GroupDim.Size);
                var runtimeGroupThreadIdx = relativeThreadIdx % groupThreadSize;
                var runtimeGroupIdx = relativeThreadIdx / groupThreadSize;
                var numRuntimeGroups = NumThreads / groupThreadSize;
                var numUsedThreads = numRuntimeGroups * groupThreadSize;
                Debug.Assert(numUsedThreads > 0, "Invalid group size");

                // Check whether we are an active thread
                if (relativeThreadIdx < numUsedThreads)
                {
                    // Bind the context to the current thread
                    var groupContext = groupContexts[runtimeGroupIdx];
                    groupContext.MakeCurrent();
                    var runtimeDimension = task.RuntimeDimension;
                    var chunkSize = (runtimeDimension + numRuntimeGroups - 1) / numRuntimeGroups;
                    chunkSize = ((chunkSize + groupThreadSize - 1) / groupThreadSize) * groupThreadSize;
                    var chunkOffset = chunkSize * runtimeGroupIdx;

                    // Prepare execution
                    groupContext.WaitForNextThreadIndex();

                    var targetDimension = Math.Min(task.UserDimension, runtimeDimension);
                    Debug.Assert(groupContext.SharedMemory.LengthInBytes == task.SharedMemSize, "Invalid shared-memory initialization");
                    task.Execute(
                        groupContext,
                        runtimeGroupThreadIdx,
                        groupThreadSize,
                        chunkSize,
                        chunkOffset,
                        targetDimension);
                }

                finishedEvent.SignalAndWait();
            }
        }

        /// <summary>
        /// Generates a dynamic kernel-launcher method that will be just-in-time compiled
        /// during the first invocation. Using the generated launcher lowers the overhead
        /// for kernel launching dramatically, since unnecessary operations (like boxing)
        /// can be avoided.
        /// </summary>
        /// <param name="kernel">The kernel to generate a launcher for.</param>
        /// <param name="customGroupSize">The custom group size for the launching operation.</param>
        /// <returns>The generated launcher method.</returns>
        private MethodInfo GenerateKernelLauncherMethod(ILCompiledKernel kernel, int customGroupSize)
        {
            var entryPoint = kernel.EntryPoint;
            AdjustAndVerifyKernelGroupSize(ref customGroupSize, entryPoint);

            var launcher = entryPoint.CreateLauncherMethod(Context);
            var emitter = new ILEmitter(launcher.ILGenerator);

            var cpuKernel = emitter.DeclareLocal(typeof(CPUKernel));
            KernelLauncherBuilder.EmitLoadKernelArgument<CPUKernel, ILEmitter>(
                Kernel.KernelInstanceParamIdx, emitter);
            emitter.Emit(LocalOperation.Store, cpuKernel);

            // Create an instance of the custom task type
            var task = emitter.DeclareLocal(kernel.TaskType);
            {
                var sharedMemSize = KernelLauncherBuilder.EmitSharedMemorySizeComputation(entryPoint, emitter);

                emitter.Emit(LocalOperation.Load, cpuKernel);
                emitter.EmitCall(
                    typeof(CPUKernel).GetProperty(
                        nameof(CPUKernel.KernelExecutionDelegate),
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetGetMethod(true));

                // Load custom user dimension
                KernelLauncherBuilder.EmitLoadDimensions(
                    entryPoint,
                    emitter,
                    Kernel.KernelParamDimensionIdx,
                    () => emitter.EmitNewObject(
                        typeof(Index3).GetConstructor(
                            new Type[] { typeof(int), typeof(int), typeof(int) })));

                // Load dimensions as index3 arguments
                KernelLauncherBuilder.EmitLoadDimensions(
                    entryPoint,
                    emitter,
                    Kernel.KernelParamDimensionIdx,
                    () => emitter.EmitNewObject(
                        typeof(Index3).GetConstructor(
                            new Type[] { typeof(int), typeof(int), typeof(int) })),
                    customGroupSize);

                // Load shared-memory size
                emitter.Emit(LocalOperation.Load, sharedMemSize);

                // Create new task object
                emitter.EmitNewObject(kernel.TaskConstructor);

                // Store task
                emitter.Emit(LocalOperation.Store, task);
            }

            // Assign parameters
            var parameters = entryPoint.Parameters;
            for (int i = 0, e = parameters.NumParameters; i < e; ++i)
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

        /// <summary cref="Accelerator.EstimateMaxActiveGroupsPerMultiprocessor(Kernel, int, int)"/>
        protected override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes)
        {
            var cpuKernel = kernel as CPUKernel;
            if (cpuKernel == null)
                throw new NotSupportedException("Not supported kernel");

            return NumThreads / groupSize;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(Kernel, Func{int, int}, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize)
        {
            var cpuKernel = kernel as CPUKernel;
            if (cpuKernel == null)
                throw new NotSupportedException("Not supported kernel");

            // Estimation
            minGridSize = NumThreads;
            return 1;
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(Kernel, int, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            var cpuKernel = kernel as CPUKernel;
            if (cpuKernel == null)
                throw new NotSupportedException("Not supported kernel");

            // Estimation
            minGridSize = NumThreads;
            return 1;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

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
            groupContexts = null;
            finishedEvent.Dispose();
        }

        #endregion
    }
}
