// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.Backends.IL.Transformations;
using ILGPU.Backends.Velocity.Transformations;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Transformations;
using ILGPU.Runtime;
using ILGPU.Runtime.Velocity;
using ILGPU.Util;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// Represents an automatic vectorization backend to be used with Velocity.
    /// </summary>
    /// <typeparam name="TILEmitter">The IL emitter type.</typeparam>
    class VelocityBackend<TILEmitter> :
        CodeGeneratorBackend<
        VelocityBackend<TILEmitter>.Handler,
        VelocityCodeGenerator<TILEmitter>.GeneratorArgs,
        VelocityCodeGenerator<TILEmitter>,
        object>
        where TILEmitter : struct, IILEmitter
    {
        #region Nested Types

        /// <summary>
        /// Represents the handler delegate type of custom code-generation handlers.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="value">The value to generate code for.</param>
        public delegate void Handler(
            VelocityBackend<TILEmitter> backend,
            in TILEmitter emitter,
            Value value);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new Velocity backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="warpSize">The current warp size.</param>
        /// <param name="argumentMapper">The argument mapper to use.</param>
        /// <param name="specializer">The specializer to generate instructions.</param>
        public VelocityBackend(
            Context context,
            VelocityCapabilityContext capabilities,
            int warpSize,
            VelocityArgumentMapper argumentMapper,
            VelocityTargetSpecializer specializer)
            : base(
                context,
                capabilities,
                BackendType.Velocity,
                argumentMapper)
        {
            WarpSize = warpSize;
            Specializer = specializer;
            TypeGenerator = specializer.CreateTypeGenerator(
                capabilities,
                context.RuntimeSystem);

            InitIntrinsicProvider();
            InitializeKernelTransformers(builder =>
            {
                var transformerBuilder = Transformer.CreateBuilder(
                    TransformerConfiguration.Empty);
                transformerBuilder.AddBackendOptimizations<CodePlacement.GroupOperands>(
                    new ILAcceleratorSpecializer(
                        AcceleratorType.Velocity,
                        PointerType,
                        warpSize,
                        Context.Properties.EnableAssertions,
                        enableIOOperations: false),
                    context.Properties.InliningMode,
                    context.Properties.OptimizationLevel);

                // Transform all if and switch branches to make them compatible with
                // the internal vectorization engine
                transformerBuilder.Add(new VelocityBlockScheduling());
                transformerBuilder.Add(new DeadCodeElimination());

                builder.Add(transformerBuilder.ToTransformer());
            });
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current warp size to be used.
        /// </summary>
        public int WarpSize { get; }

        /// <summary>
        /// Returns the current specializer.
        /// </summary>
        internal VelocityTargetSpecializer Specializer { get; }

        /// <summary>
        /// Returns the current type generator.
        /// </summary>
        internal VelocityTypeGenerator TypeGenerator { get; }

        /// <summary>
        /// Returns the associated <see cref="ArgumentMapper"/>.
        /// </summary>
        public new VelocityArgumentMapper ArgumentMapper =>
            base.ArgumentMapper.AsNotNullCast<VelocityArgumentMapper>();

        #endregion

        [SuppressMessage(
            "Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Module will be disposed during finalization")]
        protected override object CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out VelocityCodeGenerator<TILEmitter>.GeneratorArgs data)
        {
            // Create a new generation module
            var module = new VelocityGenerationModule(
                Context.RuntimeSystem,
                Specializer,
                TypeGenerator,
                backendContext,
                entryPoint);
            data = new VelocityCodeGenerator<TILEmitter>.GeneratorArgs(
                Specializer,
                module,
                entryPoint);
            return null!;
        }

        protected override VelocityCodeGenerator<TILEmitter>
            CreateFunctionCodeGenerator(
            Method method,
            Allocas allocas,
            VelocityCodeGenerator<TILEmitter>.GeneratorArgs data) =>
            new VelocityFunctionGenerator<TILEmitter>(data, method, allocas);

        protected override VelocityCodeGenerator<TILEmitter>
            CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Allocas allocas,
            VelocityCodeGenerator<TILEmitter>.GeneratorArgs data) =>
            new VelocityKernelFunctionGenerator<TILEmitter>(
                data,
                method,
                allocas);

        protected override CompiledKernel CreateKernel(
            EntryPoint entryPoint,
            CompiledKernel.KernelInfo? kernelInfo,
            object builder,
            VelocityCodeGenerator<TILEmitter>.GeneratorArgs data)
        {
            using var module = data.Module;
            return new VelocityCompiledKernel(
                Context,
                entryPoint,
                module.KernelMethod,
                module.ParametersType,
                module.ParametersTypeConstructor,
                module.ParameterFields,
                module.SharedAllocationSize);
        }
    }
}
