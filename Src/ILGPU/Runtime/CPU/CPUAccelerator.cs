// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CPUAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Compiler;
using ILGPU.Resources;
using ILGPU.Util;
using System;
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
        #region Instance

        private readonly AssemblyBuilder assemblyBuilder;
        private readonly ModuleBuilder moduleBuilder;
        private int typeBuilderIdx = 0;

        private Thread[] threads;
        private CPURuntimeWarpContext[] warpContexts;
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
        /// <param name="flags">The compile-unit flags.</param>
        public CPUAccelerator(Context context, CompileUnitFlags flags)
            : this(context, Environment.ProcessorCount, flags)
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
        /// <param name="flags">The compile-unit flags.</param>
        public CPUAccelerator(Context context, int numThreads, CompileUnitFlags flags)
            : this(context, numThreads, ThreadPriority.Normal, flags)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        public CPUAccelerator(Context context, int numThreads, int warpSize)
            : this(context, numThreads, warpSize, ThreadPriority.Normal)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        /// <param name="flags">The compile-unit flags.</param>
        public CPUAccelerator(
            Context context,
            int numThreads,
            int warpSize,
            CompileUnitFlags flags)
            : this(context, numThreads, warpSize, ThreadPriority.Normal, flags)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        public CPUAccelerator(Context context, int numThreads, ThreadPriority threadPriority)
            : this(context, numThreads, 1, threadPriority)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        /// <param name="flags">The compile-unit flags.</param>
        public CPUAccelerator(
            Context context,
            int numThreads,
            ThreadPriority threadPriority,
            CompileUnitFlags flags)
            : this(context, numThreads, 1, threadPriority, flags)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        public CPUAccelerator(Context context, int numThreads, int warpSize, ThreadPriority threadPriority)
            : this(context, numThreads, warpSize, threadPriority, DefaultFlags)
        { }

        /// <summary>
        /// Constructs a new CPU runtime.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        /// <param name="flags">The compile-unit flags.</param>
        public CPUAccelerator(
            Context context,
            int numThreads,
            int warpSize,
            ThreadPriority threadPriority,
            CompileUnitFlags flags)
            : base(context, AcceleratorType.CPU)
        {
            if (numThreads < 1)
                throw new ArgumentOutOfRangeException(nameof(numThreads));
            if (!CPURuntimeWarpContext.IsValidWarpSize(warpSize) || numThreads < warpSize || (numThreads % warpSize) != 0)
                throw new ArgumentOutOfRangeException(nameof(warpSize));

            // Setup assembly and module builder for dynamic code generation
            var assemblyName = new AssemblyName(nameof(CPUAccelerator));

            assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule(nameof(CPUAccelerator));

            NumThreads = numThreads;
            WarpSize = warpSize;
            threads = new Thread[numThreads];
            finishedEvent = new Barrier(numThreads + 1);
            // Every thread requires a custom warp context.
            warpContexts = new CPURuntimeWarpContext[numThreads];
            // The maximum number of thread groups that can be handled in parallel is
            // equal to the number of available threads in the worst case.
            groupContexts = new CPURuntimeGroupContext[numThreads];
            for (int i = 0; i < numThreads; ++i)
            {
                warpContexts[i] = new CPURuntimeWarpContext(this);
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

            Bind();
            InitBackend(CreateBackend(), flags);
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

        /// <summary cref="Accelerator.CreateBackend"/>
        public override Backend CreateBackend()
        {
            return new MSILBackend(Context);
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
            var launcherMethod = GenerateKernelLauncherMethod(
                kernel,
                out Type taskType,
                out FieldInfo[] taskArgumentMapping,
                customGroupSize);
            var executionMethod = GenerateKernelExecutionMethod(
                kernel,
                taskType,
                taskArgumentMapping);
            return new CPUKernel(
                this,
                kernel,
                launcherMethod,
                (CPUKernelExecutionHandler)executionMethod.CreateDelegate(
                    typeof(CPUKernelExecutionHandler)));
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

            // Setup global grid and group indices
            Grid.Dimension = task.GridDim;
            Group.Dimension = task.GroupDim;

            // Setup groups
            var numRuntimeGroups = NumThreads / groupThreadSize;
            for (int i = 0; i < numRuntimeGroups; ++i)
            {
                var context = groupContexts[i];
                context.Initialize(task.GroupDim, task.SharedMemSize);
            }

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
            var warpContext = warpContexts[relativeThreadIdx / WarpSize];
            Debug.Assert(warpContext != null, "Invalid warp context");
            warpContext.MakeCurrent();

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
                warpContext.Initialize(runtimeGroupThreadIdx, out int runtimeThreadOffset);
                var runtimeGroupIdx = relativeThreadIdx / groupThreadSize;
                var numRuntimeGroups = NumThreads / groupThreadSize;
                var numUsedThreads = numRuntimeGroups * groupThreadSize;
                Debug.Assert(numUsedThreads > 0, "Invalid group size");

                // Check whether we are an active thread
                if (relativeThreadIdx < numUsedThreads)
                {
                    // Bind the context to the current thread
                    groupContexts[runtimeGroupIdx].MakeCurrent(out ArrayView<byte> sharedMemory, out Barrier groupBarrier);
                    var runtimeDimension = task.RuntimeDimension;
                    var chunkSize = (runtimeDimension + numRuntimeGroups - 1) / numRuntimeGroups;
                    chunkSize = ((chunkSize + groupThreadSize - 1) / groupThreadSize) * groupThreadSize;
                    var chunkOffset = chunkSize * runtimeGroupIdx;

                    var targetDimension = Math.Min(task.UserDimension, runtimeDimension);
                    Debug.Assert(sharedMemory.LengthInBytes == task.SharedMemSize, "Invalid shared-memory initialization");
                    task.Execute(
                        groupBarrier,
                        sharedMemory,
                        runtimeThreadOffset,
                        groupThreadSize,
                        numRuntimeGroups,
                        numUsedThreads,
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
        /// <param name="taskType">The created task.</param>
        /// <param name="taskArgumentMapping">The created task-argument mapping that maps parameter indices of uniforms</param>
        /// <param name="customGroupSize">The custom group size for the launching operation.</param>
        /// <returns>The generated launcher method.</returns>
        private MethodInfo GenerateKernelLauncherMethod(
            CompiledKernel kernel,
            out Type taskType,
            out FieldInfo[] taskArgumentMapping,
            int customGroupSize)
        {
            var entryPoint = kernel.EntryPoint;

            if (customGroupSize < 0)
                throw new ArgumentOutOfRangeException(nameof(customGroupSize));

            if (entryPoint.IsGroupedIndexEntry)
            {
                if (customGroupSize > 0)
                    throw new InvalidOperationException(RuntimeErrorMessages.InvalidCustomGroupSize);
            }
            else if (customGroupSize == 0)
                customGroupSize = WarpSize;

            var uniformVariables = entryPoint.UniformVariables;
            var numUniformVariables = uniformVariables.Length;

            var kernelParamTypes = entryPoint.CreateCustomParameterTypes();
            int numKernelParams = kernelParamTypes.Length;
            var funcParamTypes = new Type[numKernelParams + Kernel.KernelParameterOffset];

            GenerateAcceleratorTask(
                kernel,
                kernelParamTypes,
                out taskType,
                out ConstructorInfo taskConstructor,
                out taskArgumentMapping);

            // Launcher(Kernel, AcceleratorStream, [Index], ...)
            funcParamTypes[Kernel.KernelInstanceParamIdx] = typeof(Kernel);
            funcParamTypes[Kernel.KernelStreamParamIdx] = typeof(AcceleratorStream);
            funcParamTypes[Kernel.KernelParamDimensionIdx] = entryPoint.KernelIndexType;
            kernelParamTypes.CopyTo(funcParamTypes, Kernel.KernelParameterOffset);

            // Create the actual launcher method
            var func = new DynamicMethod(
                kernel.EntryName,
                typeof(void),
                funcParamTypes,
                typeof(Kernel));
            var funcParams = func.GetParameters();
            var ilGenerator = func.GetILGenerator();

            var cpuKernel = ilGenerator.DeclareLocal(typeof(CPUKernel));
            KernelLauncherBuilder.EmitLoadKernelArgument<CPUKernel>(Kernel.KernelInstanceParamIdx, ilGenerator);
            ilGenerator.Emit(OpCodes.Stloc, cpuKernel);

            // Create an instance of the custom task type
            var task = ilGenerator.DeclareLocal(taskType);
            {
                var sharedMemSize = KernelLauncherBuilder.EmitSharedMemorySizeComputation(
                    entryPoint,
                    ilGenerator,
                    paramIdx => funcParams[paramIdx + Kernel.KernelParameterOffset]);

                ilGenerator.Emit(OpCodes.Ldloc, cpuKernel);
                ilGenerator.Emit(
                    OpCodes.Call,
                    typeof(CPUKernel).GetProperty(
                        nameof(CPUKernel.KernelExecutionDelegate),
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetGetMethod(true));

                // Load custom user dimension
                KernelLauncherBuilder.EmitLoadDimensions(
                    entryPoint,
                    ilGenerator,
                    Kernel.KernelParamDimensionIdx,
                    () => ilGenerator.Emit(
                        OpCodes.Newobj,
                        typeof(Index3).GetConstructor(
                            new Type[] { typeof(int), typeof(int), typeof(int) })));

                // Load dimensions as index3 arguments
                KernelLauncherBuilder.EmitLoadDimensions(
                    entryPoint,
                    ilGenerator,
                    Kernel.KernelParamDimensionIdx,
                    () => ilGenerator.Emit(
                        OpCodes.Newobj,
                        typeof(Index3).GetConstructor(
                            new Type[] { typeof(int), typeof(int), typeof(int) })),
                    customGroupSize);

                // Load shared-memory size
                ilGenerator.Emit(OpCodes.Ldloc, sharedMemSize);

                // Create new task object
                ilGenerator.Emit(OpCodes.Newobj, taskConstructor);

                // Store task
                ilGenerator.Emit(OpCodes.Stloc, task);
            }

            // Assign parameters
            for (int i = 0; i < numUniformVariables; ++i)
            {
                ilGenerator.Emit(OpCodes.Ldloc, task);
                ilGenerator.Emit(OpCodes.Ldarg, i + Kernel.KernelParameterOffset);
                ilGenerator.Emit(OpCodes.Stfld, taskArgumentMapping[i]);
            }

            // Launch task: ((CPUKernel)kernel).CPUAccelerator.Launch(task);
            ilGenerator.Emit(OpCodes.Ldloc, cpuKernel);
            ilGenerator.Emit(
                OpCodes.Call,
                typeof(CPUKernel).GetProperty(
                    nameof(CPUKernel.CPUAccelerator)).GetGetMethod(false));
            ilGenerator.Emit(OpCodes.Ldloc, task);
            ilGenerator.Emit(
                OpCodes.Call,
                typeof(CPUAccelerator).GetMethod(
                    nameof(CPUAccelerator.Launch),
                    BindingFlags.NonPublic | BindingFlags.Instance));

            // End of launch method
            ilGenerator.Emit(OpCodes.Ret);

            return func;
        }

        /// <summary>
        /// Generates specialized task classes for kernel execution.
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        /// <param name="kernelParamTypes">The required launch parameter types.</param>
        /// <param name="taskType">The created task.</param>
        /// <param name="taskConstructor">The created task constructor.</param>
        /// <param name="taskArgumentMapping">The created task-argument mapping that maps parameter indices of uniforms
        /// and dynamically-sized shared-memory-variable-length specifications to fields in the task class.</param>
        private void GenerateAcceleratorTask(
            CompiledKernel kernel,
            Type[] kernelParamTypes,
            out Type taskType,
            out ConstructorInfo taskConstructor,
            out FieldInfo[] taskArgumentMapping)
        {
            var acceleratorTaskType = typeof(CPUAcceleratorTask);

            var typeIdx = typeBuilderIdx++;
            var taskBuilder = moduleBuilder.DefineType($"KernelTask{typeIdx}",
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                acceleratorTaskType);

            var ctor = taskBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.HasThis,
                CPUAcceleratorTask.ConstructorParameterTypes);
            // Build constructor
            {
                var constructorILGenerator = ctor.GetILGenerator();
                constructorILGenerator.Emit(OpCodes.Ldarg_0);
                for (int i = 0, e = CPUAcceleratorTask.ConstructorParameterTypes.Length; i < e; ++i)
                    constructorILGenerator.Emit(OpCodes.Ldarg, i + 1);
                constructorILGenerator.Emit(
                    OpCodes.Call,
                    acceleratorTaskType.GetConstructor(
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        CPUAcceleratorTask.ConstructorParameterTypes,
                        null));
                constructorILGenerator.Emit(OpCodes.Ret);
            }

            // Define all fields
            var argFieldBuilders = new FieldInfo[kernelParamTypes.Length];
            for (int i = 0, e = argFieldBuilders.Length; i < e; ++i)
            {
                argFieldBuilders[i] = taskBuilder.DefineField(
                    $"Arg{i}",
                    kernelParamTypes[i],
                    FieldAttributes.Public);
            }

            taskType = taskBuilder.CreateType();
            taskConstructor = taskType.GetConstructor(CPUAcceleratorTask.ConstructorParameterTypes);

            // Map the final fields
            taskArgumentMapping = new FieldInfo[kernelParamTypes.Length];
            for (int i = 0, e = taskArgumentMapping.Length; i < e; ++i)
                taskArgumentMapping[i] = taskType.GetField(argFieldBuilders[i].Name);
        }

        /// <summary>
        /// Generates specialized task classes for kernel execution.
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        /// <param name="taskType">The created task.</param>
        /// <param name="taskArgumentMapping">The created task-argument mapping that maps parameter indices of uniforms
        /// and dynamically-sized shared-memory-variable-length specifications to fields in the task class.</param>
        private static MethodInfo GenerateKernelExecutionMethod(
            CompiledKernel kernel,
            Type taskType,
            FieldInfo[] taskArgumentMapping)
        {
            var entryPoint = kernel.EntryPoint;
            var ungroupedIndexType = entryPoint.UngroupedIndexType;

            // Build execute method
            var execute = new DynamicMethod(
                $"Execute_{kernel.EntryName}",
                typeof(void),
                CPUAcceleratorTask.ExecuteParameterTypes,
                taskType,
                true);

            // Build execute body
            var ilGenerator = execute.GetILGenerator();

            // Cast generic task type to actual task type
            var task = ilGenerator.DeclareLocal(taskType);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Castclass, taskType);
            ilGenerator.Emit(OpCodes.Stloc, task);

            // Determine used grid dimensions
            var gridDim = ilGenerator.DeclareLocal(ungroupedIndexType);
            var groupDimSize = ilGenerator.DeclareLocal(typeof(int));
            var groupDim = ilGenerator.DeclareLocal(ungroupedIndexType);
            {
                var getGridDimFromTask = typeof(CPUAcceleratorTask).GetProperty(
                    nameof(CPUAcceleratorTask.UserGridDim)).GetGetMethod(false);

                KernelLauncherBuilder.EmitConvertIndex3ToTargetType(
                    ungroupedIndexType, ilGenerator, () =>
                    {
                        ilGenerator.Emit(OpCodes.Ldarg_0);
                        ilGenerator.Emit(OpCodes.Call, getGridDimFromTask);
                    });
                ilGenerator.Emit(OpCodes.Stloc, gridDim);

                var getGroupDimFromTask = typeof(CPUAcceleratorTask).GetProperty(
                    nameof(CPUAcceleratorTask.GroupDim)).GetGetMethod(false);

                KernelLauncherBuilder.EmitConvertIndex3ToTargetType(
                    ungroupedIndexType, ilGenerator, () =>
                    {
                        ilGenerator.Emit(OpCodes.Ldloc, task);
                        ilGenerator.Emit(OpCodes.Call, getGroupDimFromTask);
                    });
                ilGenerator.Emit(OpCodes.Stloc, groupDim);

                // Compute linear group-dim size
                ilGenerator.Emit(OpCodes.Ldloca, groupDim);
                ilGenerator.Emit(
                    OpCodes.Call,
                    ungroupedIndexType.GetProperty(nameof(IIndex.Size)).GetGetMethod());
                ilGenerator.Emit(OpCodes.Stloc, groupDimSize);
            }

            // Cache all fields in local variables
            var taskArgumentLocals = new LocalBuilder[taskArgumentMapping.Length];
            var numUniformVariables = entryPoint.NumUniformVariables;
            Debug.Assert(numUniformVariables <= taskArgumentLocals.Length);

            for (int i = 0, e = taskArgumentLocals.Length; i < e; ++i)
            {
                // Declare local
                taskArgumentLocals[i] = ilGenerator.DeclareLocal(
                    taskArgumentMapping[i].FieldType);

                // Load instance field i
                ilGenerator.Emit(OpCodes.Ldloc, task);
                ilGenerator.Emit(OpCodes.Ldfld, taskArgumentMapping[i]);

                // Cache field value in local variable
                ilGenerator.Emit(OpCodes.Stloc, taskArgumentLocals[i]);
            }

            // Cache types of shared-memory variable
            var sharedMemVariables = entryPoint.SharedMemoryVariables;

            // Initialize sharedMemOffset to 0
            var sharedMemOffset = ilGenerator.DeclareLocal(typeof(int));
            ilGenerator.Emit(OpCodes.Ldc_I4_0);
            ilGenerator.Emit(OpCodes.Stloc, sharedMemOffset);

            var sharedMemoryLocals = new LocalBuilder[sharedMemVariables.Length];
            int dynamicallySizedLengthIdx = 0;
            for (int i = 0, e = sharedMemVariables.Length; i < e; ++i)
            {
                var sharedMemVariable = sharedMemVariables[i];
                sharedMemoryLocals[i] = ilGenerator.DeclareLocal(sharedMemVariable.Type);
                var lengthLocal = ilGenerator.DeclareLocal(typeof(int));

                // The length of dynamically-sized shared-memory variables has to be loaded
                // from the provided length fields
                if (sharedMemVariable.IsDynamicallySizedArray)
                {
                    ilGenerator.Emit(OpCodes.Ldloc, taskArgumentLocals[numUniformVariables + dynamicallySizedLengthIdx++]);
                    ilGenerator.Emit(OpCodes.Stloc, lengthLocal);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldc_I4, sharedMemVariable.Size);
                    ilGenerator.Emit(OpCodes.Stloc, lengthLocal);
                }

                // Load the shared-memory-view from the parameter with index 1
                ilGenerator.Emit(OpCodes.Ldarga, 2);

                // Load offset & length
                ilGenerator.Emit(OpCodes.Ldloc, sharedMemOffset);
                KernelLauncherBuilder.EmitLoadIndex(ilGenerator);
                ilGenerator.Emit(OpCodes.Ldloc, lengthLocal);
                KernelLauncherBuilder.EmitLoadIndex(ilGenerator);

                // var tView = sharedMemory.GetSubView(offset, length)
                ilGenerator.Emit(OpCodes.Call, typeof(ArrayView<byte>).GetMethod(nameof(ArrayView<byte>.GetSubView),
                    new Type[] { typeof(Index), typeof(Index) }));

                // local = tView.BitCast<TargetType>
                // or
                // local = tView.BitCast<TargetType>().GetVariableView();

                var castLocal = ilGenerator.DeclareLocal(typeof(ArrayView<byte>));
                ilGenerator.Emit(OpCodes.Stloc, castLocal);
                ilGenerator.Emit(OpCodes.Ldloca, castLocal);
                ilGenerator.Emit(OpCodes.Call,
                    typeof(ArrayView<byte>).GetMethod(nameof(ArrayView<byte>.Cast)).MakeGenericMethod(
                        sharedMemVariable.ElementType));

                if (!sharedMemVariable.IsArray)
                {
                    var genericArrayViewType = typeof(ArrayView<>).MakeGenericType(
                        sharedMemVariable.ElementType);
                    var castedLocal = ilGenerator.DeclareLocal(genericArrayViewType);
                    ilGenerator.Emit(OpCodes.Stloc, castedLocal);
                    ilGenerator.Emit(OpCodes.Ldloca, castedLocal);

                    ilGenerator.Emit(OpCodes.Call,
                        genericArrayViewType.GetMethod(
                            nameof(ArrayView<byte>.GetVariableView),
                            new Type[] { }));
                }

                // Store shared-memory view to local variable
                ilGenerator.Emit(OpCodes.Stloc, sharedMemoryLocals[i]);

                // Add the current size to the memory offset
                ilGenerator.Emit(OpCodes.Ldloc, sharedMemOffset);
                ilGenerator.Emit(OpCodes.Ldloc, lengthLocal);
                ilGenerator.Emit(OpCodes.Add);
                ilGenerator.Emit(OpCodes.Stloc, sharedMemOffset);
            }

            // Build loop to address all dispatched grid indices
            var loopHeader = ilGenerator.DefineLabel();
            var loopBody = ilGenerator.DefineLabel();

            // Init counter: int i = WarpSize * runtimeWarpId + runtimeWarpThreadIdx;
            // => int i = runtimeWarpOffset + runtimeWarpThreadIdx
            // => int i = runtimeThreadOffset
            var chunkIdxCounter = ilGenerator.DeclareLocal(typeof(int));
            var breakCondition = ilGenerator.DeclareLocal(typeof(bool));
            ilGenerator.Emit(OpCodes.Ldarg, 3);
            ilGenerator.Emit(OpCodes.Stloc, chunkIdxCounter);
            ilGenerator.Emit(OpCodes.Br, loopHeader);

            var globalIndex = ilGenerator.DeclareLocal(typeof(int));

            // Loop body
            {
                ilGenerator.MarkLabel(loopBody);

                // var index = i + chunkOffset;
                ilGenerator.Emit(OpCodes.Ldloc, chunkIdxCounter);
                ilGenerator.Emit(OpCodes.Ldarg, 8);
                ilGenerator.Emit(OpCodes.Add);

                ilGenerator.Emit(OpCodes.Stloc, globalIndex);
            }

            // Check the custom user dimension
            // globalIndex < targetDimension
            var kernelNotInvoked = ilGenerator.DefineLabel();
            ilGenerator.Emit(OpCodes.Ldloc, globalIndex);
            ilGenerator.Emit(OpCodes.Ldarg, 9);
            ilGenerator.Emit(OpCodes.Clt);
            ilGenerator.Emit(OpCodes.Stloc, breakCondition);
            ilGenerator.Emit(OpCodes.Ldloc, breakCondition);
            ilGenerator.Emit(OpCodes.Brfalse, kernelNotInvoked);

            // Launch the actual kernel method
            {
                // Construct launch index from linear index
                ilGenerator.Emit(OpCodes.Ldloc, globalIndex);
                if (!entryPoint.IsGroupedIndexEntry)
                {
                    // Use direct construction for 1D index
                    KernelLauncherBuilder.EmitConvertFrom1DIndexToTargetIndexType(
                        ungroupedIndexType,
                        ilGenerator,
                        () => ilGenerator.Emit(OpCodes.Ldloc, gridDim));
                }
                else
                {
                    // We have to split grid and group indices for GroupedIndex-reconstruction
                    var linearIdx = ilGenerator.DeclareLocal(typeof(int));
                    ilGenerator.Emit(OpCodes.Stloc, linearIdx);

                    // Compute grid index
                    ilGenerator.Emit(OpCodes.Ldloc, linearIdx);
                    ilGenerator.Emit(OpCodes.Ldloc, groupDimSize);
                    ilGenerator.Emit(OpCodes.Div);
                    KernelLauncherBuilder.EmitConvertFrom1DIndexToTargetIndexType(
                        ungroupedIndexType,
                        ilGenerator,
                        () => ilGenerator.Emit(OpCodes.Ldloc, gridDim));

                    // Compute group index
                    ilGenerator.Emit(OpCodes.Ldloc, linearIdx);
                    ilGenerator.Emit(OpCodes.Ldloc, groupDimSize);
                    ilGenerator.Emit(OpCodes.Rem);
                    KernelLauncherBuilder.EmitConvertFrom1DIndexToTargetIndexType(
                        ungroupedIndexType,
                        ilGenerator,
                        () => ilGenerator.Emit(OpCodes.Ldloc, groupDim));

                    var groupedConstructor = entryPoint.KernelIndexType.GetConstructor(
                        new Type[] { ungroupedIndexType, ungroupedIndexType });
                    ilGenerator.Emit(OpCodes.Newobj, groupedConstructor);
                }

                // Load kernel arguments
                var variableReferences = new LocalBuilder[entryPoint.NumCustomParameters];
                for (int i = 0; i < numUniformVariables; ++i)
                    variableReferences[entryPoint.UniformVariables[i].Index - 1] = taskArgumentLocals[i];
                for (int i = 0, e = sharedMemoryLocals.Length; i < e; ++i)
                    variableReferences[entryPoint.SharedMemoryVariables[i].Index - 1] = sharedMemoryLocals[i];

                // Load kernel arguments
                foreach (var variableRef in variableReferences)
                {
                    Debug.Assert(variableRef != null, "Invalid kernel argument");
                    ilGenerator.Emit(OpCodes.Ldloc, variableRef);
                }

                // Invoke kernel
                ilGenerator.Emit(OpCodes.Call, entryPoint.MethodInfo);
            }

            // Synchronize group threads
            {
                ilGenerator.MarkLabel(kernelNotInvoked);

                // Memory barrier for interlocked calls
                ilGenerator.Emit(
                    OpCodes.Call,
                    typeof(Thread).GetMethod(
                        nameof(Thread.MemoryBarrier),
                        BindingFlags.Public | BindingFlags.Static));

                // Wait for other group threads
                ilGenerator.Emit(OpCodes.Ldarg, 1);
                ilGenerator.Emit(
                    OpCodes.Call,
                    typeof(Barrier).GetMethod(
                        nameof(Barrier.SignalAndWait),
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new Type[] { },
                        null));
            }

            // Increase counter
            {
                // i += groupSize
                ilGenerator.Emit(OpCodes.Ldloc, chunkIdxCounter);
                ilGenerator.Emit(OpCodes.Ldarg, 4);
                ilGenerator.Emit(OpCodes.Add);
                ilGenerator.Emit(OpCodes.Stloc, chunkIdxCounter);
            }

            // Loop header
            {
                ilGenerator.MarkLabel(loopHeader);

                // if (i < chunkSize) ...
                ilGenerator.Emit(OpCodes.Ldloc, chunkIdxCounter);
                ilGenerator.Emit(OpCodes.Ldarg, 7);
                ilGenerator.Emit(OpCodes.Clt);
                ilGenerator.Emit(OpCodes.Stloc, breakCondition);
                ilGenerator.Emit(OpCodes.Ldloc, breakCondition);
                ilGenerator.Emit(OpCodes.Brtrue, loopBody);
            }

            // Emit final return
            ilGenerator.Emit(OpCodes.Ret);

            return execute;
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
            foreach (var warp in warpContexts)
                warp.Dispose();
            warpContexts = null;
            foreach (var group in groupContexts)
                group.Dispose();
            groupContexts = null;
            finishedEvent.Dispose();
        }

        #endregion
    }
}
