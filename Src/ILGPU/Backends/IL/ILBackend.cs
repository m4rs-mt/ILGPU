// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ILBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// The basic MSIL backend for the CPU runtime.
    /// </summary>
    public abstract class ILBackend : Backend
    {
        #region Nested Types

        /// <summary>
        /// Contains important global variable references.
        /// </summary>
        protected internal sealed class KernelGenerationData
        {
            #region Properties

            /// <summary>
            /// Maps the grid dimension.
            /// </summary>
            public ILLocal UserGridDim { get; set; }

            /// <summary>
            /// Maps the current thread index.
            /// </summary>
            public ILLocal Index { get; set; }

            /// <summary>
            /// The intrinsic kernel invocation variable.
            /// </summary>
            public ILLabel KernelNotInvoked { get; set; }

            /// <summary>
            /// The current loop header.
            /// </summary>
            public ILLabel LoopHeader { get; set; }

            /// <summary>
            /// The current loop body.
            /// </summary>
            public ILLabel LoopBody { get; set; }

            /// <summary>
            /// The chunk index counter.
            /// </summary>
            public ILLocal ChunkIdxCounter { get; set; }

            /// <summary>
            /// The loop break condition.
            /// </summary>
            public ILLocal BreakCondition { get; set; }

            /// <summary>
            /// The uniform references.
            /// </summary>
            public ImmutableArray<ILLocal> Uniforms { get; private set; }

            #endregion

            /// <summary>
            /// Setups the given uniform variables.
            /// </summary>
            /// <param name="uniformVariables">The variables to setup.</param>
            public void SetupUniforms(ImmutableArray<ILLocal> uniformVariables)
            {
                Debug.Assert(Uniforms.IsDefaultOrEmpty);
                Uniforms = uniformVariables;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new IL backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="backendFlags">The backend flags.</param>
        /// <param name="warpSize">The current warp size.</param>
        /// <param name="argumentMapper">The argument mapper to use.</param>
        internal ILBackend(
            Context context,
            CapabilityContext capabilities,
            BackendFlags backendFlags,
            int warpSize,
            ArgumentMapper argumentMapper)
            : base(
                  context,
                  capabilities,
                  BackendType.IL,
                  backendFlags,
                  argumentMapper)
        {
            WarpSize = warpSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated warp size.
        /// </summary>
        public int WarpSize { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new <see cref="ILCompiledKernel"/> instance.
        /// </summary>
        protected sealed override CompiledKernel Compile(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization)
        {
            var taskType = GenerateAcceleratorTask(
                entryPoint.Parameters,
                out ConstructorInfo taskConstructor,
                out ImmutableArray<FieldInfo> taskArgumentMapping);

            MethodInfo kernelMethod;
            using (var scopedLock = RuntimeSystem.DefineRuntimeMethod(
                typeof(void),
                CPUAcceleratorTask.ExecuteParameterTypes,
                out var methodEmitter))
            {
                var emitter = new ILEmitter(methodEmitter.ILGenerator);
                var kernelData = new KernelGenerationData();

                // Generate CPU runtime startup code
                GenerateStartupCode(
                    entryPoint,
                    emitter,
                    kernelData,
                    taskType,
                    taskArgumentMapping);

                // Generate the actual kernel code
                GenerateCode(
                    entryPoint,
                    backendContext,
                    emitter,
                    kernelData);

                // Generate CPU runtime finish code
                GenerateFinishCode(
                    emitter,
                    kernelData);

                emitter.Finish();
                kernelMethod = methodEmitter.Finish();
            }

            return new ILCompiledKernel(
                Context,
                entryPoint,
                kernelMethod,
                taskType,
                taskConstructor,
                taskArgumentMapping);
        }

        /// <summary>
        /// Generates the actual kernel code.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="entryPoint">The desired entry point.</param>
        /// <param name="backendContext">The current backend context.</param>
        /// <param name="emitter">The current code generator.</param>
        /// <param name="kernelData">The current kernel data.</param>
        protected abstract void GenerateCode<TEmitter>(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            TEmitter emitter,
            KernelGenerationData kernelData)
            where TEmitter : IILEmitter;

        #endregion

        #region Kernel Functionality

        /// <summary>
        /// Generates specialized task classes for kernel execution.
        /// </summary>codeEmitter
        /// <param name="parameters">The parameter collection.</param>
        /// <param name="taskConstructor">The created task constructor.</param>
        /// <param name="taskArgumentMapping">
        /// The created task-argument mapping that maps parameter indices of uniforms
        /// and dynamically-sized shared-memory-variable-length specifications to fields
        /// in the task class.
        /// </param>
        private Type GenerateAcceleratorTask(
            in ParameterCollection parameters,
            out ConstructorInfo taskConstructor,
            out ImmutableArray<FieldInfo> taskArgumentMapping)
        {
            const string ArgumentFormat = "Arg{0}";

            var acceleratorTaskType = typeof(CPUAcceleratorTask);
            var argFieldBuilders = new FieldInfo[parameters.Count];

            Type taskType;
            {
                using var scopedLock = RuntimeSystem.DefineRuntimeClass(
                    acceleratorTaskType,
                    out var taskBuilder);

                var ctor = taskBuilder.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.HasThis,
                    CPUAcceleratorTask.ConstructorParameterTypes);

                // Build constructor
                {
                    var constructorILGenerator = ctor.GetILGenerator();
                    constructorILGenerator.Emit(OpCodes.Ldarg_0);
                    for (
                        int i = 0,
                        e = CPUAcceleratorTask.ConstructorParameterTypes.Length;
                        i < e;
                        ++i)
                    {
                        constructorILGenerator.Emit(OpCodes.Ldarg, i + 1);
                    }
                    constructorILGenerator.Emit(
                        OpCodes.Call,
                        CPUAcceleratorTask.GetTaskConstructor(acceleratorTaskType));
                    constructorILGenerator.Emit(OpCodes.Ret);
                }

                // Define all fields
                for (int i = 0, e = argFieldBuilders.Length; i < e; ++i)
                {
                    taskBuilder.DefineField(
                        string.Format(ArgumentFormat, i),
                        parameters[i],
                        FieldAttributes.Public);
                }

                // Create the actual type
                taskType = taskBuilder.CreateType();

                // Get all fields
                for (int i = 0, e = argFieldBuilders.Length; i < e; ++i)
                {
                    argFieldBuilders[i] = taskBuilder.GetField(
                        string.Format(ArgumentFormat, i));
                }
            }
            taskConstructor = taskType.GetConstructor(
                CPUAcceleratorTask.ConstructorParameterTypes);

            // Map the final fields
            var resultMapping = ImmutableArray.CreateBuilder<FieldInfo>(
                parameters.Count);
            for (int i = 0, e = parameters.Count; i < e; ++i)
                resultMapping.Add(taskType.GetField(argFieldBuilders[i].Name));
            taskArgumentMapping = resultMapping.MoveToImmutable();

            return taskType;
        }

        /// <summary>
        /// Generates kernel startup code.
        /// </summary>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="emitter">The current code generator.</param>
        /// <param name="kernelData">The current kernel data.</param>
        /// <param name="taskType">The created task.</param>
        /// <param name="taskArgumentMapping">
        /// The created task-argument mapping that maps parameter indices of uniforms
        /// and dynamically-sized shared-memory-variable-length specifications to fields
        /// in the task class.
        /// </param>
        private void GenerateStartupCode<TEmitter>(
            EntryPoint entryPoint,
            TEmitter emitter,
            KernelGenerationData kernelData,
            Type taskType,
            ImmutableArray<FieldInfo> taskArgumentMapping)
            where TEmitter : IILEmitter
        {
            // Cast generic task type to actual task type
            var task = emitter.DeclareLocal(taskType);
            emitter.Emit(OpCodes.Ldarg_0);
            emitter.Emit(OpCodes.Castclass, taskType);
            emitter.Emit(LocalOperation.Store, task);

            // Store the grid and group dimension of the current task
            var sourceGridDim = emitter.DeclareLocal(typeof(Index3));
            emitter.Emit(LocalOperation.Load, task);
            emitter.EmitCall(typeof(CPUAcceleratorTask).GetProperty(
                nameof(CPUAcceleratorTask.UserGridDim)).GetGetMethod(false));
            emitter.Emit(LocalOperation.Store, sourceGridDim);

            var sourceGroupDim = emitter.DeclareLocal(typeof(Index3));
            emitter.Emit(LocalOperation.Load, task);
            emitter.EmitCall(typeof(CPUAcceleratorTask).GetProperty(
                nameof(CPUAcceleratorTask.GroupDim)).GetGetMethod(false));
            emitter.Emit(LocalOperation.Store, sourceGroupDim);

            // Determine used grid dimensions
            if (entryPoint.IsImplictlyGrouped)
            {
                kernelData.UserGridDim = emitter.DeclareLocal(
                    entryPoint.KernelIndexType);

                KernelLauncherBuilder.EmitConvertIndex3ToTargetType(
                    entryPoint.IndexType, emitter,
                    () => emitter.Emit(LocalOperation.Load, sourceGridDim));
                emitter.Emit(LocalOperation.Store, kernelData.UserGridDim);
            }

            // Compute group dimension size
            var groupDimSize = emitter.DeclareLocal(typeof(int));
            emitter.Emit(LocalOperation.Load, task);
            emitter.EmitCall(typeof(CPUAcceleratorTask).GetProperty(
                nameof(CPUAcceleratorTask.GroupDimSize)).GetGetMethod(false));
            emitter.Emit(LocalOperation.Store, groupDimSize);

            GenerateLocals(entryPoint, emitter, kernelData, taskArgumentMapping, task);

            // Build loop to address all dispatched grid indices
            kernelData.LoopHeader = emitter.DeclareLabel();
            kernelData.LoopBody = emitter.DeclareLabel();

            // Init counter: int i = runtimeThreadOffset
            kernelData.ChunkIdxCounter = emitter.DeclareLocal(typeof(int));
            kernelData.BreakCondition = emitter.DeclareLocal(typeof(bool));
            emitter.Emit(
                ArgumentOperation.Load,
                CPUAcceleratorTask.RuntimeThreadOffsetIndex);
            emitter.Emit(LocalOperation.Store, kernelData.ChunkIdxCounter);
            emitter.Emit(OpCodes.Br, kernelData.LoopHeader);

            var globalIndex = emitter.DeclareLocal(typeof(int));

            // Loop body
            {
                emitter.MarkLabel(kernelData.LoopBody);

                // var index = i + chunkOffset;
                emitter.Emit(LocalOperation.Load, kernelData.ChunkIdxCounter);
                emitter.Emit(
                    ArgumentOperation.Load,
                    CPUAcceleratorTask.ChunkSizeOffsetIndex);
                emitter.Emit(OpCodes.Add);

                emitter.Emit(LocalOperation.Store, globalIndex);
            }

            // Check the custom user dimension
            // globalIndex < targetDimension
            kernelData.KernelNotInvoked = emitter.DeclareLabel();
            emitter.Emit(LocalOperation.Load, globalIndex);
            emitter.Emit(
                ArgumentOperation.Load,
                CPUAcceleratorTask.TargetDimensionIndex);
            emitter.Emit(OpCodes.Clt);
            emitter.Emit(LocalOperation.Store, kernelData.BreakCondition);
            emitter.Emit(LocalOperation.Load, kernelData.BreakCondition);
            emitter.Emit(OpCodes.Brfalse, kernelData.KernelNotInvoked);

            // Compute linear grid index
            var linearGridIndex = emitter.DeclareLocal(typeof(int));
            emitter.Emit(LocalOperation.Load, globalIndex);
            emitter.Emit(LocalOperation.Load, groupDimSize);
            emitter.Emit(OpCodes.Div);
            emitter.Emit(LocalOperation.Store, linearGridIndex);

            // Compute linear group index
            var linearGroupIndex = emitter.DeclareLocal(typeof(int));
            emitter.Emit(LocalOperation.Load, globalIndex);
            emitter.Emit(LocalOperation.Load, groupDimSize);
            emitter.Emit(OpCodes.Rem);
            emitter.Emit(LocalOperation.Store, linearGroupIndex);

            // Bind current multi-dimensional grid and group indices
            emitter.Emit(LocalOperation.Load, linearGridIndex);
            KernelLauncherBuilder.EmitConvertFrom1DIndexToTargetIndexType(
                IndexType.Index3D,
                emitter,
                () => emitter.Emit(LocalOperation.Load, sourceGridDim));

            emitter.Emit(LocalOperation.Load, linearGroupIndex);
            KernelLauncherBuilder.EmitConvertFrom1DIndexToTargetIndexType(
                IndexType.Index3D,
                emitter,
                () => emitter.Emit(LocalOperation.Load, sourceGroupDim));
            emitter.EmitCall(CPURuntimeThreadContext.SetupIndicesMethod);

            if (entryPoint.IsImplictlyGrouped)
            {
                // Construct launch index from linear index
                kernelData.Index = emitter.DeclareLocal(entryPoint.KernelIndexType);

                // Use direct construction for 1D index
                emitter.Emit(LocalOperation.Load, globalIndex);
                KernelLauncherBuilder.EmitConvertFrom1DIndexToTargetIndexType(
                    entryPoint.IndexType,
                    emitter,
                    () => emitter.Emit(LocalOperation.Load, kernelData.UserGridDim));
                emitter.Emit(LocalOperation.Store, kernelData.Index);
            }
            else
            {
                // Do nothing
            }
        }

        /// <summary>
        /// Generates the required local variables (e.g. shared memory).
        /// </summary>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="emitter">The current code generator.</param>
        /// <param name="kernelData">The current kernel data.</param>
        /// <param name="taskArgumentMapping">
        /// The created task-argument mapping that maps parameter indices of uniforms.
        /// </param>
        /// <param name="task">The task variable.</param>
        protected abstract void GenerateLocals<TEmitter>(
            EntryPoint entryPoint,
            TEmitter emitter,
            KernelGenerationData kernelData,
            ImmutableArray<FieldInfo> taskArgumentMapping,
            ILLocal task)
            where TEmitter : IILEmitter;

        /// <summary>
        /// Generates the kernel finish code.
        /// </summary>
        /// <param name="emitter">The current code generator.</param>
        /// <param name="kernelData">The current kernel data.</param>
        private static void GenerateFinishCode<TEmitter>(
            TEmitter emitter,
            KernelGenerationData kernelData)
            where TEmitter : IILEmitter
        {
            // Synchronize group threads
            {
                emitter.MarkLabel(kernelData.KernelNotInvoked);

                // Wait for all threads to complete and reset all required information
                emitter.Emit(
                    ArgumentOperation.Load,
                    CPUAcceleratorTask.GroupContextIndex);
                emitter.EmitCall(RuntimeMethods.WaitForNextThreadIndex);
            }

            // Increase counter
            {
                // i += groupSize
                emitter.Emit(LocalOperation.Load, kernelData.ChunkIdxCounter);
                emitter.Emit(
                    ArgumentOperation.Load,
                    CPUAcceleratorTask.RuntimeGroupSizeIndex);
                emitter.Emit(OpCodes.Add);
                emitter.Emit(LocalOperation.Store, kernelData.ChunkIdxCounter);
            }

            // Loop header
            {
                emitter.MarkLabel(kernelData.LoopHeader);

                // if (i < chunkSize) ...
                emitter.Emit(LocalOperation.Load, kernelData.ChunkIdxCounter);
                emitter.Emit(ArgumentOperation.Load, CPUAcceleratorTask.ChunkSizeIndex);
                emitter.Emit(OpCodes.Clt);
                emitter.Emit(LocalOperation.Store, kernelData.BreakCondition);
                emitter.Emit(LocalOperation.Load, kernelData.BreakCondition);
                emitter.Emit(OpCodes.Brtrue, kernelData.LoopBody);
            }

            // Emit final return
            emitter.Emit(OpCodes.Ret);
        }

        #endregion
    }
}
