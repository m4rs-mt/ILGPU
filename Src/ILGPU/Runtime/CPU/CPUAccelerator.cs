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
using System.Threading;

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
    /// Specifies a simulator kind of a <see cref="CPUAccelerator"/> instance.
    /// </summary>
    public enum CPUAcceleratorKind
    {
        /// <summary>
        /// A CPU accelerator that simulates a common configuration of a default GPU
        /// simulator with 1 multiprocessor, a warp size of 4 and 4 warps per
        /// multiprocessor.
        /// </summary>
        Default,

        /// <summary>
        /// a CPU accelerator that simulates a common configuration of an NVIDIA GPU
        /// with 1 multiprocessor.
        /// </summary>
        Nvidia,

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of an AMD GPU with
        /// 1 multiprocessor.
        /// </summary>
        AMD,

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of a legacy GCN AMD
        /// GPU with 1 multiprocessor.
        /// </summary>
        LegacyAMD,

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of an Intel GPU
        /// with 1 multiprocessor.
        /// </summary>
        Intel
    }

    /// <summary>
    /// Represents a general CPU-based runtime for kernels.
    /// </summary>
    public sealed partial class CPUAccelerator : Accelerator
    {
        #region Constants

        /// <summary>
        /// The default warp size of 4 threads per group.
        /// </summary>
        private const int DefaultWarpSize = 4;

        /// <summary>
        /// The default number of 4 warps per multiprocessor.
        /// </summary>
        private const int DefaultNumWarpsPerMultiprocessor = 4;

        /// <summary>
        /// The default number of 1 multiprocessor.
        /// </summary>
        private const int DefaultNumMultiprocessors = 1;

        #endregion

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
        /// Maps <see cref="CPUAcceleratorKind"/> values to <see cref="CPUAccelerator"/>
        /// static factory methods.
        /// </summary>
        private static readonly Func<Context, CPUAcceleratorMode, CPUAccelerator>[]
            CreateAccelerators =
        {
            CreateDefaultSimulator,
            CreateNvidiaSimulator,
            CreateAMDSimulator,
            CreateLegacyAMDSimulator,
            CreateIntelSimulator,
        };

        #endregion

        #region Static Creation

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of a default
        /// GPU simulator with 1 multiprocessor, a warp size of 4 and 4 warps per
        /// multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        /// <remarks>
        /// Note that this accelerator will use <see cref="CPUAcceleratorMode.Auto"/>.
        /// </remarks>
        public static CPUAccelerator CreateDefaultSimulator(Context context) =>
            CreateDefaultSimulator(context, CPUAcceleratorMode.Auto);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of a default
        /// GPU simulator with 1 multiprocessor, a warp size of 4 and 4 warps per
        /// multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="mode">The accelerator mode.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateDefaultSimulator(
            Context context,
            CPUAcceleratorMode mode) =>
            new CPUAccelerator(context, mode);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an NVIDIA
        /// GPU with 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        /// <remarks>
        /// Note that this accelerator will use <see cref="CPUAcceleratorMode.Auto"/>.
        /// </remarks>
        public static CPUAccelerator CreateNvidiaSimulator(Context context) =>
            CreateNvidiaSimulator(context, CPUAcceleratorMode.Auto);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an AMD
        /// GPU with 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        /// <remarks>
        /// Note that this accelerator will use <see cref="CPUAcceleratorMode.Auto"/>.
        /// </remarks>
        public static CPUAccelerator CreateAMDSimulator(Context context) =>
            CreateAMDSimulator(context, CPUAcceleratorMode.Auto);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of a legacy
        /// GCN AMD GPU with 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        /// <remarks>
        /// Note that this accelerator will use <see cref="CPUAcceleratorMode.Auto"/>.
        /// </remarks>
        public static CPUAccelerator CreateLegacyAMDSimulator(Context context) =>
            CreateLegacyAMDSimulator(context, CPUAcceleratorMode.Auto);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an Intel
        /// GPU with 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        /// <remarks>
        /// Note that this accelerator will use <see cref="CPUAcceleratorMode.Auto"/>.
        /// </remarks>
        public static CPUAccelerator CreateIntelSimulator(Context context) =>
            CreateIntelSimulator(context, CPUAcceleratorMode.Auto);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an NVIDIA
        /// GPU with 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="mode">The accelerator mode.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateNvidiaSimulator(
            Context context,
            CPUAcceleratorMode mode) =>
            new CPUAccelerator(context, 32, 32, 1, mode);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an AMD
        /// GPU with 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="mode">The accelerator mode.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateAMDSimulator(
            Context context,
            CPUAcceleratorMode mode) =>
            new CPUAccelerator(context, 32, 8, 1, mode);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of a legacy
        /// GCN AMD GPU with 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="mode">The accelerator mode.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateLegacyAMDSimulator(
            Context context,
            CPUAcceleratorMode mode) =>
            new CPUAccelerator(context, 64, 4, 1, mode);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration of an Intel
        /// GPU with 1 multiprocessor.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="mode">The accelerator mode.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator CreateIntelSimulator(
            Context context,
            CPUAcceleratorMode mode) =>
            new CPUAccelerator(context, 16, 8, 1, mode);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration specified
        /// by the given <paramref name="kind"/>.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="kind">The desired CPU accelerator kind.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        /// <remarks>
        /// Note that this accelerator will use <see cref="CPUAcceleratorMode.Auto"/>.
        /// </remarks>
        public static CPUAccelerator Create(Context context, CPUAcceleratorKind kind) =>
            Create(context, kind, CPUAcceleratorMode.Auto);

        /// <summary>
        /// Creates a CPU accelerator that simulates a common configuration specified
        /// by the given <paramref name="kind"/>.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="kind">The desired CPU accelerator kind.</param>
        /// <param name="mode">The accelerator mode.</param>
        /// <returns>The created CPU accelerator instance.</returns>
        public static CPUAccelerator Create(
            Context context,
            CPUAcceleratorKind kind,
            CPUAcceleratorMode mode) =>
            kind < CPUAcceleratorKind.Default || kind > CPUAcceleratorKind.Intel
            ? throw new ArgumentOutOfRangeException(nameof(kind))
            : CreateAccelerators[(int)kind](context, mode);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new CPU runtime with the default number of 4 threads per warp,
        /// 4 warps per multiprocessor and 1 multiprocessor.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        public CPUAccelerator(Context context)
            : this(
                  context,
                  DefaultWarpSize,
                  DefaultNumWarpsPerMultiprocessor,
                  DefaultNumMultiprocessors)
        { }

        /// <summary>
        /// Constructs a new CPU runtime with the default number of 4 threads per warp,
        /// 4 warps per multiprocessor and 1 multiprocessor.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="mode">The accelerator mode.</param>
        public CPUAccelerator(Context context, CPUAcceleratorMode mode)
            : this(
                  context,
                  DefaultWarpSize,
                  DefaultNumWarpsPerMultiprocessor,
                  DefaultNumMultiprocessors,
                  mode)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">
        /// The number of threads for parallel processing.
        /// </param>
        [Obsolete("Use an explicit constructor instead with the number of threads, " +
            "number of warps and number of multiprocessors. The number of threads in " +
            "this constructor will not be used anymore.")]
        public CPUAccelerator(Context context, int numThreads)
            : this(
                  context,
                  DefaultWarpSize,
                  DefaultNumWarpsPerMultiprocessor,
                  DefaultNumMultiprocessors)
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
        [Obsolete("Use an explicit constructor instead with the number of threads, " +
            "number of warps and number of multiprocessors. The number of threads in " +
            "this constructor will not be used anymore.")]
        public CPUAccelerator(
            Context context,
            int numThreads,
            ThreadPriority threadPriority)
            : this(
                  context,
                  DefaultWarpSize,
                  DefaultNumWarpsPerMultiprocessor,
                  DefaultNumMultiprocessors,
                  CPUAcceleratorMode.Auto)
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
                  CPUAcceleratorMode.Auto)
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
        /// <param name="mode">The accelerator mode.</param>
        public CPUAccelerator(
            Context context,
            int numThreadsPerWarp,
            int numWarpsPerMultiprocessor,
            int numMultiprocessors,
            CPUAcceleratorMode mode)
            : this(
                  context,
                  numThreadsPerWarp,
                  numWarpsPerMultiprocessor,
                  numMultiprocessors,
                  mode,
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
        /// <param name="mode">The current accelerator mode.</param>
        /// <param name="threadPriority">
        /// The thread priority of the execution threads.
        /// </param>
        public CPUAccelerator(
            Context context,
            int numThreadsPerWarp,
            int numWarpsPerMultiprocessor,
            int numMultiprocessors,
            CPUAcceleratorMode mode,
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
            MaxGroupSize = new Index3(
                MaxNumThreadsPerGroup,
                MaxNumThreadsPerGroup,
                MaxNumThreadsPerGroup);

            MemorySize = long.MaxValue;
            MaxGridSize = new Index3(int.MaxValue, int.MaxValue, int.MaxValue);
            MaxSharedMemoryPerGroup = int.MaxValue;
            MaxConstantMemory = int.MaxValue;

            DefaultStream = CreateStream();
            Name = nameof(CPUAccelerator);

            NumThreads = MaxNumThreads * numMultiprocessors;
            Mode = mode;
            UsesSequentialExecution =
                Mode == CPUAcceleratorMode.Sequential ||
                Mode == CPUAcceleratorMode.Auto && Debugger.IsAttached;

            Bind();
            InitExecutionEngine(numWarpsPerMultiprocessor, threadPriority);
            Init(context.DefautltILBackend);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current mode.
        /// </summary>
        public CPUAcceleratorMode Mode { get; }

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
        protected override void DisposeAccelerator_SyncRoot(bool disposing) =>
            DisposeExecutionEngine(disposing);

        #endregion
    }
}
