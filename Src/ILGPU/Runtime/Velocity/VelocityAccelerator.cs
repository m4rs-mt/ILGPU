// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.IL;
using ILGPU.Backends.Velocity;
using ILGPU.Resources;
using ILGPU.Runtime.CPU;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// A SIMD-enabled CPU-based accelerator.
    /// </summary>
    public sealed class VelocityAccelerator : Accelerator
    {
        #region Static

        /// <summary>
        /// The internal run method to launch kernels.
        /// </summary>
        private static readonly MethodInfo RunMethodInfo =
            typeof(VelocityAccelerator).GetMethod(
                nameof(Run),
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        #endregion

        #region Instance

        private readonly VelocityMultiprocessor[] multiprocessors;

        [SuppressMessage(
            "Microsoft.Usage",
            "CA2213: Disposable fields should be disposed",
            Justification = "This is disposed in DisposeAccelerator_SyncRoot")]
        private readonly SemaphoreSlim taskConcurrencyLimit = new SemaphoreSlim(1);

        [SuppressMessage(
            "Microsoft.Usage",
            "CA2213: Disposable fields should be disposed",
            Justification = "This is disposed in DisposeAccelerator_SyncRoot")]
        private readonly Barrier multiprocessorBarrier;

        /// <summary>
        /// Constructs a new Velocity accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="device">The Velocity device.</param>
        /// <param name="threadPriority">
        /// The thread priority of the execution threads.
        /// </param>
        internal VelocityAccelerator(
            Context context,
            VelocityDevice device,
            ThreadPriority threadPriority)
            : base(context, device)
        {
            if (!device.IsLittleEndian)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.VelocityLittleEndian);
            }

            NativePtr = new IntPtr(2);
            DefaultStream = CreateStreamInternal();

            multiprocessors = new VelocityMultiprocessor[device.NumMultiprocessors];
            multiprocessorBarrier = new Barrier(device.NumMultiprocessors + 1);
            ThreadPriority = threadPriority;
            MaxLocalMemoryPerThread = device.MaxLocalMemoryPerThread;
            NumThreads = device.WarpSize * device.NumMultiprocessors;

            // Initialize all multiprocessors
            Action<VelocityMultiprocessor> processingCompleted = OnProcessingCompleted;
            for (int i = 0; i < device.NumMultiprocessors; ++i)
            {
                var multiProcessor = new VelocityMultiprocessor(this, i);
                multiProcessor.ProcessingCompleted += processingCompleted;
                multiprocessors[i] = multiProcessor;
            }

            // Init the underlying Velocity backend
            Init(new VelocityBackend<ILEmitter>(
                context,
                new CPUCapabilityContext(),
                WarpSize,
                new VelocityArgumentMapper(context)));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Velocity backend of this accelerator.
        /// </summary>
        internal new VelocityBackend<ILEmitter> Backend =>
            base.Backend as VelocityBackend<ILEmitter>;

        /// <summary>
        /// Returns the current thread priority.
        /// </summary>
        public ThreadPriority ThreadPriority { get; }

        /// <summary>
        /// Returns the maximum local memory per thread in bytes.
        /// </summary>
        public int MaxLocalMemoryPerThread { get; }

        /// <summary>
        /// Returns the maximum number of parallel threads.
        /// </summary>
        public int NumThreads { get; }

        #endregion

        #region Launch Methods

        /// <summary>
        /// Main internal run method to launch loaded kernels.
        /// </summary>
        /// <param name="userKernelConfig">The user-defined kernel config.</param>
        /// <param name="runtimeKernelConfig">
        /// The actual runtime kernel config to be used for launching.
        /// </param>
        /// <param name="kernelHandler">The kernel entry point handler.</param>
        /// <param name="velocityParameters">
        /// The current velocity kernel parameters.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Run(
            KernelConfig userKernelConfig,
            RuntimeKernelConfig runtimeKernelConfig,
            VelocityKernelEntryPoint kernelHandler,
            VelocityParameters velocityParameters)
        {
            // Avoid concurrent executions of kernels.. we have to wait for the current
            // kernel to finish first
            taskConcurrencyLimit.Wait();

            // Determine actual thread-grid sizes
            int gridSize = runtimeKernelConfig.GridDim.Size;
            int groupSize = runtimeKernelConfig.GroupDim.Size;
            Debug.Assert(groupSize <= WarpSize, "Invalid group size");

            // Distribute the workload
            int numActiveMPs = Math.Min(
                IntrinsicMath.DivRoundUp(gridSize, NumMultiprocessors),
                NumMultiprocessors);

            // Compute the chunk size per multiprocessor and adjust it to be a multiple
            // of the warp size to avoid holes in the processing grid
            int chunkSizePerMP = IntrinsicMath.DivRoundUp(gridSize, numActiveMPs);
            chunkSizePerMP *= groupSize;

            // Setup multiprocessor barrier
            int upperBound = numActiveMPs + 1;
            int participantCount = multiprocessorBarrier.ParticipantCount;
            if (participantCount > upperBound)
                multiprocessorBarrier.RemoveParticipants(participantCount - upperBound);
            else if (participantCount < upperBound)
                multiprocessorBarrier.AddParticipants(upperBound - participantCount);

            try
            {
                // Start the multiprocessor journey
                int totalGridSize = Math.Min(
                    gridSize * groupSize,
                    (int)userKernelConfig.Size);
                for (int i = 0; i < numActiveMPs; ++i)
                {
                    int startIndex = i * chunkSizePerMP;
                    int endIndex = Math.Min(startIndex + chunkSizePerMP, totalGridSize);
                    multiprocessors[i].Run(
                        kernelHandler,
                        startIndex,
                        endIndex,
                        gridSize,
                        groupSize,
                        velocityParameters);
                }

                // Wait for all multiprocessors to finish
                multiprocessorBarrier.SignalAndWait();
            }
            finally
            {
                taskConcurrencyLimit.Release();
            }
        }

        /// <summary>
        /// Once processing is completed in the scope of each multiprocessor,
        /// </summary>
        /// <param name="processor"></param>
        private void OnProcessingCompleted(VelocityMultiprocessor processor) =>
            multiprocessorBarrier.SignalAndWait();

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
            VelocityCompiledKernel kernel,
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

            // Declare a new launcher method
            using var scopedLock = entryPoint.CreateLauncherMethod(
                Context.RuntimeSystem,
                out var launcher);
            var emitter = new ILEmitter(launcher.ILGenerator);

            // Map all arguments to an argument structure containing mapped views
            var argumentMapper = Backend.ArgumentMapper;
            var (structLocal, _) = argumentMapper.Map(emitter, entryPoint);

            var velocityKernel = emitter.DeclareLocal(typeof(VelocityKernel));
            KernelLauncherBuilder.EmitLoadKernelArgument<VelocityKernel, ILEmitter>(
                Kernel.KernelInstanceParamIdx, emitter);
            emitter.Emit(LocalOperation.Store, velocityKernel);

            // Create an instance of the custom parameters type
            var parametersInstance = emitter.DeclarePinnedLocal(kernel.ParametersType);
            emitter.Emit(OpCodes.Ldnull);
            emitter.Emit(LocalOperation.Store, parametersInstance);
            {
                // Assign parameters
                var parameters = entryPoint.Parameters;
                for (int i = 0, e = parameters.Count; i < e; ++i)
                {
                    // Load native address onto stack
                    emitter.Emit(LocalOperation.LoadAddress, structLocal);
                    emitter.LoadFieldAddress(structLocal.VariableType, i);
                    emitter.Emit(OpCodes.Conv_I);
                }

                // Create new task object
                emitter.EmitNewObject(kernel.ParametersTypeConstructor);

                // Store task
                emitter.Emit(LocalOperation.Store, parametersInstance);
            }

            // Load the kernel delegate
            emitter.Emit(LocalOperation.Load, velocityKernel);
            emitter.EmitCall(VelocityKernel.GetVelocityAccelerator);

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

            // Load the kernel delegate
            emitter.Emit(LocalOperation.Load, velocityKernel);
            emitter.EmitCall(VelocityKernel.GetKernelExecutionDelegate);

            // Load the parameters object
            emitter.Emit(LocalOperation.Load, parametersInstance);

            // Launch kernel execution
            emitter.EmitCall(RunMethodInfo);

            // End of launch method
            emitter.Emit(OpCodes.Ret);
            emitter.Finish();

            return launcher.Finish();
        }

        #endregion

        /// <inheritdoc/>
        public override TExtension CreateExtension<
            TExtension,
            TExtensionProvider>(TExtensionProvider provider) =>
            provider.CreateVelocityExtension(this);

        /// <inheritdoc/>
        protected override MemoryBuffer AllocateRawInternal(
            long length,
            int elementSize) =>
            new VelocityMemoryBuffer(this, length, elementSize);

        /// <summary>
        /// Loads the given kernel.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="customGroupSize">The custom group size.</param>
        /// <returns>The loaded kernel</returns>
        private Kernel LoadKernel(CompiledKernel kernel, int customGroupSize)
        {
            if (kernel is null)
                throw new ArgumentNullException(nameof(kernel));
            if (!(kernel is VelocityCompiledKernel compiledKernel))
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedKernel);
            }

            var launcherMethod = GenerateKernelLauncherMethod(
                compiledKernel,
                customGroupSize);
            return new VelocityKernel(
                this,
                compiledKernel,
                launcherMethod);
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
            new VelocityStream(this);

        /// <summary cref="Accelerator.Synchronize"/>
        protected override void SynchronizeInternal() { }

        /// <summary cref="Accelerator.OnBind"/>
        protected override void OnBind() { }

        /// <summary cref="Accelerator.OnUnbind"/>
        protected override void OnUnbind() { }

        #region Peer Access

        /// <summary cref="Accelerator.CanAccessPeerInternal(Accelerator)"/>
        protected override bool CanAccessPeerInternal(Accelerator otherAccelerator) =>
            otherAccelerator is CPUAccelerator ||
            otherAccelerator is VelocityAccelerator;

        /// <summary cref="Accelerator.EnablePeerAccessInternal(Accelerator)"/>
        protected override void EnablePeerAccessInternal(Accelerator otherAccelerator)
        {
            if (!CanAccessPeerInternal(otherAccelerator))
            {
                throw new InvalidOperationException(
                    RuntimeErrorMessages.CannotEnablePeerAccessToOtherAccelerator);
            }
        }

        /// <summary cref="Accelerator.DisablePeerAccessInternal(Accelerator)"/>
        protected override void DisablePeerAccessInternal(
            Accelerator otherAccelerator) =>
            Debug.Assert(
                CanAccessPeerInternal(otherAccelerator),
                "Invalid EnablePeerAccess method");

        #endregion

        #region Occupancy

        /// <summary cref="Accelerator.EstimateMaxActiveGroupsPerMultiprocessor(
        /// Kernel, int, int)"/>
        protected override int EstimateMaxActiveGroupsPerMultiprocessorInternal(
            Kernel kernel,
            int groupSize,
            int dynamicSharedMemorySizeInBytes) =>
            kernel is VelocityKernel
            ? groupSize > MaxGroupSize.Size ? 0 : NumMultiprocessors
            : throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(
        /// Kernel, Func{int, int}, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            Func<int, int> computeSharedMemorySize,
            int maxGroupSize,
            out int minGridSize)
        {
            if (!(kernel is VelocityKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            // Estimation
            minGridSize = NumThreads;
            return Math.Min(maxGroupSize, MaxGroupSize.Size);
        }

        /// <summary cref="Accelerator.EstimateGroupSizeInternal(
        /// Kernel, int, int, out int)"/>
        protected override int EstimateGroupSizeInternal(
            Kernel kernel,
            int dynamicSharedMemorySizeInBytes,
            int maxGroupSize,
            out int minGridSize)
        {
            if (!(kernel is VelocityKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);

            // Estimation
            minGridSize = NumThreads;
            return 1;
        }

        #endregion

        #region Page Lock Scope

        /// <inheritdoc/>
        protected override PageLockScope<T> CreatePageLockFromPinnedInternal<T>(
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
            taskConcurrencyLimit.Wait();

            // Dispose all multiprocessors
            foreach (var multiprocessor in multiprocessors)
                multiprocessor.Dispose();

            // Dispose barriers
            taskConcurrencyLimit.Dispose();
            multiprocessorBarrier.Dispose();
        }

        #endregion

    }
}
