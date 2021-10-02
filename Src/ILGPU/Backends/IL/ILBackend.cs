// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL.Transformations;
using ILGPU.IR;
using ILGPU.IR.Transformations;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// The basic MSIL backend for the CPU runtime.
    /// </summary>
    public abstract class ILBackend : Backend<ILBackend.Handler>
    {
        #region Nested Types

        /// <summary>
        /// Represents the handler delegate type of custom code-generation handlers.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="value">The value to generate code for.</param>
        public delegate void Handler(
            ILBackend backend,
            in ILEmitter emitter,
            Value value);

        #endregion

        #region Static

        /// <summary>
        /// A reference to the static <see cref="Reconstruct2DIndex(Index2D, int)"/>
        /// method.
        /// </summary>
        private static readonly MethodInfo Reconstruct2DIndexMethod =
            typeof(ILBackend).GetMethod(
                nameof(Reconstruct2DIndex),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// A reference to the static <see cref="Reconstruct3DIndex(Index3D, int)"/>
        /// method.
        /// </summary>
        private static readonly MethodInfo Reconstruct3DIndexMethod =
            typeof(ILBackend).GetMethod(
                nameof(Reconstruct3DIndex),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Helper method to reconstruct 2D indices.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Index2D Reconstruct2DIndex(Index2D totalDim, int linearIndex) =>
            Stride2D.DenseX.ReconstructFromElementIndex(linearIndex, totalDim);

        /// <summary>
        /// Helper method to reconstruct 3D indices.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Index3D Reconstruct3DIndex(Index3D totalDim, int linearIndex) =>
            Stride3D.DenseXY.ReconstructFromElementIndex(linearIndex, totalDim);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new IL backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="warpSize">The current warp size.</param>
        /// <param name="argumentMapper">The argument mapper to use.</param>
        internal ILBackend(
            Context context,
            CapabilityContext capabilities,
            int warpSize,
            ArgumentMapper argumentMapper)
            : base(
                  context,
                  capabilities,
                  BackendType.IL,
                  argumentMapper)
        {
            WarpSize = warpSize;

            InitIntrinsicProvider();
            InitializeKernelTransformers(builder =>
            {
                var transformerBuilder = Transformer.CreateBuilder(
                    TransformerConfiguration.Empty);
                transformerBuilder.AddBackendOptimizations(
                    new ILAcceleratorSpecializer(
                        PointerType,
                        warpSize,
                        Context.Properties.EnableAssertions,
                        Context.Properties.EnableIOOperations),
                    context.Properties.InliningMode,
                    context.Properties.OptimizationLevel);
                builder.Add(transformerBuilder.ToTransformer());
            });
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated warp size.
        /// </summary>
        public int WarpSize { get; }

        /// <summary>
        /// Returns the associated <see cref="Backend.ArgumentMapper"/>.
        /// </summary>
        public new ILArgumentMapper ArgumentMapper =>
            base.ArgumentMapper as ILArgumentMapper;

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
            // Build the custom strongly type task type and define the kernel method
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

                // Generate CPU runtime startup code and initialize all locals
                GenerateStartupCode(
                    entryPoint,
                    emitter,
                    taskType,
                    out var taskLocal,
                    out var indexLocal);
                var locals = GenerateLocals(
                    emitter,
                    taskArgumentMapping,
                    taskLocal);

                // Generate the actual kernel code
                GenerateCode(
                    entryPoint,
                    backendContext,
                    emitter,
                    taskLocal,
                    indexLocal,
                    locals);

                // Finish building
                emitter.Emit(OpCodes.Ret);
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
        /// <param name="task">The strongly typed task local.</param>
        /// <param name="index">The index dimension local (for implicit kernels).</param>
        /// <param name="locals">
        /// The array of all local variables loaded from the task kernel implementation.
        /// </param>
        protected abstract void GenerateCode<TEmitter>(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            TEmitter emitter,
            in ILLocal task,
            in ILLocal index,
            ImmutableArray<ILLocal> locals)
            where TEmitter : IILEmitter;

        #endregion

        #region Kernel Functionality

        /// <summary>
        /// Generates code that caches all task fields in local variables.
        /// </summary>
        /// <param name="emitter">The current code generator.</param>
        /// <param name="taskArgumentMapping">
        /// The created task-argument mapping that maps parameter indices of uniforms
        /// and dynamically-sized shared-memory-variable-length specifications to fields
        /// in the task class.
        /// </param>
        /// <param name="task">The strongly typed task local.</param>
        private static ImmutableArray<ILLocal> GenerateLocals<TEmitter>(
            TEmitter emitter,
            ImmutableArray<FieldInfo> taskArgumentMapping,
            ILLocal task)
            where TEmitter : IILEmitter
        {
            // Cache all fields in local variables
            var taskArgumentLocals = ImmutableArray.CreateBuilder<ILLocal>(
                taskArgumentMapping.Length);

            for (int i = 0, e = taskArgumentMapping.Length; i < e; ++i)
            {
                var taskArgument = taskArgumentMapping[i];
                var taskArgumentType = taskArgument.FieldType;

                // Load instance field i
                emitter.Emit(LocalOperation.Load, task);
                emitter.Emit(OpCodes.Ldfld, taskArgumentMapping[i]);

                // Declare local
                taskArgumentLocals.Add(emitter.DeclareLocal(taskArgumentType));

                // Cache field value in local variable
                emitter.Emit(LocalOperation.Store, taskArgumentLocals[i]);
            }

            return taskArgumentLocals.MoveToImmutable();
        }

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
        /// <param name="taskType">The created task.</param>
        /// <param name="task">The created strongly typed task local.</param>
        /// <param name="index">The index dimension local (for implicit kernels).</param>
        private static void GenerateStartupCode<TEmitter>(
            EntryPoint entryPoint,
            TEmitter emitter,
            Type taskType,
            out ILLocal task,
            out ILLocal index)
            where TEmitter : IILEmitter
        {
            // Cast generic task type to actual task type
            task = emitter.DeclareLocal(taskType);
            emitter.Emit(OpCodes.Ldarg_0);
            emitter.Emit(OpCodes.Castclass, taskType);
            emitter.Emit(LocalOperation.Store, task);

            // Construct launch index from linear index
            index = emitter.DeclareLocal(entryPoint.KernelIndexType);
            emitter.Emit(LocalOperation.LoadAddress, index);
            emitter.Emit(OpCodes.Initobj, index.VariableType);

            if (entryPoint.IsExplicitlyGrouped)
                return;

            // Convert to the appropriate index type
            emitter.Emit(LocalOperation.Load, task);
            switch (entryPoint.IndexType)
            {
                case IndexType.Index1D:
                    // Ignore the task local and construct a new 1D instance
                    emitter.Emit(OpCodes.Pop);
                    emitter.Emit(ArgumentOperation.Load, CPUAcceleratorTask.LinearIndex);
                    emitter.EmitNewObject(Index1D.MainConstructor);
                    break;
                case IndexType.Index2D:
                    // Convert to 2D index
                    emitter.EmitCall(
                        CPUAcceleratorTask.GetTotalUserDimXYGetter(taskType));
                    emitter.Emit(ArgumentOperation.Load, CPUAcceleratorTask.LinearIndex);
                    emitter.EmitCall(Reconstruct2DIndexMethod);
                    break;
                case IndexType.Index3D:
                    // Convert to 3D index
                    emitter.EmitCall(
                        CPUAcceleratorTask.GetTotalUserDimGetter(taskType));
                    emitter.Emit(ArgumentOperation.Load, CPUAcceleratorTask.LinearIndex);
                    emitter.EmitCall(Reconstruct3DIndexMethod);
                    break;
                default:
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedIndexType);

            }
            // Store the index operation
            emitter.Emit(LocalOperation.Store, index);
        }

        #endregion
    }
}
