// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.IL;
using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

#pragma warning disable CA1508 // Avoid dead conditional code

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// The accelerator mode to be used with the <see cref="CPUAccelerator"/>.
    /// </summary>
    public enum CPUAcceleratorMode
    {
        /// <summary>
        /// The automatic mode uses <see cref="Sequential"/> if a debugger is attached.
        /// It uses <see cref="Parallel"/> if no debugger is attached to the
        /// application.
        /// </summary>
        /// <remarks>
        /// This is the default mode.
        /// </remarks>
        Auto = 0,

        /// <summary>
        /// If the CPU accelerator uses a simulated sequential execution mechanism. This
        /// is particularly useful to simplify debugging. Note that different threads for
        /// distinct multiprocessors may still run in parallel.
        /// </summary>
        Sequential = 1,

        /// <summary>
        /// A parallel execution mode that runs all execution threads in parallel. This
        /// reduces processing time but makes it harder to use a debugger.
        /// </summary>
        Parallel = 2,
    }


    /// <summary>
    /// Represents a general CPU-based runtime for kernels.
    /// </summary>
    public sealed partial class CPUAccelerator : Accelerator
    {
        #region Instance

        // General execution management

        private readonly CPUMultiprocessor[] multiprocessors;
        private volatile bool running = true;

        // Task execution

        private readonly object taskSynchronizationObject = new object();
        private volatile CPUAcceleratorTask currentTask;

        private readonly Barrier finishedEventPerMultiprocessor;
        private readonly SemaphoreSlim taskConcurrencyLimit = new SemaphoreSlim(1);

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="description">The accelerator description.</param>
        /// <param name="mode">The current accelerator mode.</param>
        /// <param name="threadPriority">
        /// The thread priority of the execution threads.
        /// </param>
        internal CPUAccelerator(
            Context context,
            CPUDevice description,
            CPUAcceleratorMode mode,
            ThreadPriority threadPriority)
            : base(context, description)
        {
            NativePtr = new IntPtr(1);

            DefaultStream = CreateStream();

            NumThreads = description.NumThreads;
            Mode = mode;
            ThreadPriority = threadPriority;
            UsesSequentialExecution =
                Mode == CPUAcceleratorMode.Sequential ||
                Mode == CPUAcceleratorMode.Auto && Debugger.IsAttached;
            finishedEventPerMultiprocessor = new Barrier(NumMultiprocessors + 1);
            multiprocessors = new CPUMultiprocessor[NumMultiprocessors];
            for (int i = 0, e = NumMultiprocessors; i < e; ++i)
            {
                multiprocessors[i] = CPUMultiprocessor.Create(
                    this,
                    i,
                    UsesSequentialExecution);
            }

            Bind();
            Init(context.DefautltILBackend);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current mode.
        /// </summary>
        public CPUAcceleratorMode Mode { get; }

        /// <summary>
        /// Returns the current thread priority.
        /// </summary>
        public ThreadPriority ThreadPriority { get; }

        /// <summary>
        /// Returns true if the current accelerator uses a simulated sequential execution
        /// mechanism. This is particularly useful to simplify debugging. Note that
        /// different threads for distinct multiprocessors may still run in parallel.
        /// </summary>
        public bool UsesSequentialExecution { get; }

        /// <summary>
        /// Returns the number of threads.
        /// </summary>
        public int NumThreads { get; }

        /// <summary>
        /// Returns the IL backend of this accelerator.
        /// </summary>
        internal new ILBackend Backend => base.Backend as ILBackend;

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override TExtension CreateExtension<
            TExtension,
            TExtensionProvider>(TExtensionProvider provider) =>
            provider.CreateCPUExtension(this);

        /// <inheritdoc/>
        protected override MemoryBuffer AllocateRawInternal(
            long length,
            int elementSize) =>
            CPUMemoryBuffer.Create(this, length, elementSize);

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

        /// <summary cref="Accelerator.DisablePeerAccessInternal(Accelerator)"/>
        protected override void DisablePeerAccessInternal(
            Accelerator otherAccelerator) =>
            Debug.Assert(
                otherAccelerator is CPUAccelerator,
                "Invalid EnablePeerAccess method");

        #endregion

        #region Launch Methods

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

            // Add support for by ref parameters
            if (entryPoint.HasByRefParameters)
            {
                throw new NotSupportedException(
                    ErrorMessages.NotSupportedByRefKernelParameters);
            }

            using var scopedLock = entryPoint.CreateLauncherMethod(
                Context.RuntimeSystem,
                out var launcher);
            var emitter = new ILEmitter(launcher.ILGenerator);

            // Pretend to map kernel arguments (like a GPU accelerator would perform).
            var argumentMapper = Backend.ArgumentMapper;
            argumentMapper.Map(entryPoint);

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
                    Kernel.KernelParamDimensionIdx,
                    MaxGridSize,
                    MaxGroupSize);

                // Load dimensions
                KernelLauncherBuilder.EmitLoadRuntimeKernelConfig(
                    entryPoint,
                    emitter,
                    Kernel.KernelParamDimensionIdx,
                    MaxGridSize,
                    MaxGroupSize,
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

        #region Execution Methods

        internal bool WaitForTask(ref CPUAcceleratorTask task)
        {
            // Get a new task to execute
            lock (taskSynchronizationObject)
            {
                while ((currentTask == null | currentTask == task) & running)
                    Monitor.Wait(taskSynchronizationObject);
                task = currentTask;
                return running;
            }
        }

        internal void FinishTaskProcessing() =>
            // Wait for the result
            finishedEventPerMultiprocessor.SignalAndWait();

        /// <summary>
        /// Launches the given accelerator task on this accelerator.
        /// </summary>
        /// <param name="task">The task to launch.</param>
        internal void Launch(CPUAcceleratorTask task)
        {
            Debug.Assert(task != null, "Invalid accelerator task");

            taskConcurrencyLimit.Wait();
            try
            {
                foreach (var multiprocessor in multiprocessors)
                    multiprocessor.InitLaunch(task);
                Interlocked.MemoryBarrier();

                // Launch all processing threads
                lock (taskSynchronizationObject)
                {
                    Debug.Assert(currentTask == null, "Invalid concurrent modification");
                    currentTask = task;
                    Monitor.PulseAll(taskSynchronizationObject);
                }

                // Wait for the result
                FinishTaskProcessing();

                // Reset all groups
                foreach (var multiprocessor in multiprocessors)
                    multiprocessor.FinishLaunch();

                // Reset task
                lock (taskSynchronizationObject)
                    currentTask = null;
            }
            finally
            {
                taskConcurrencyLimit.Release();
            }
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

        #region Page Lock Scope

        /// <inheritdoc/>
        protected unsafe override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
            IntPtr pinned,
            long numElements)
        {
            Trace.WriteLine(RuntimeErrorMessages.NotSupportedPageLock);
            return new NullPageLockScope<T>(this, pinned, numElements);
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

            // Dispose task engine
            lock (taskSynchronizationObject)
            {
                running = false;
                currentTask = null;
                Monitor.PulseAll(taskSynchronizationObject);
            }

            // Dispose all multiprocessors
            foreach (var multiprocessor in multiprocessors)
                multiprocessor.Dispose();

            // Dispose barriers
            finishedEventPerMultiprocessor.Dispose();
            taskConcurrencyLimit.Dispose();
        }

        #endregion
    }
}

#pragma warning restore CA1508 // Avoid dead conditional code
