// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.Backends.IL.Transformations;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Transformations;
using ILGPU.Runtime;
using ILGPU.Runtime.Velocity;

namespace ILGPU.Backends.Velocity
{
    class VelocityBackend<TILEmitter, TVerifier> :
        CodeGeneratorBackend<
        VelocityBackend<TILEmitter, TVerifier>.Handler,
        VelocityCodeGenerator<TILEmitter, TVerifier>.GeneratorArgs,
        VelocityCodeGenerator<TILEmitter, TVerifier>,
        object>
        where TILEmitter : struct, IILEmitter
        where TVerifier : IVelocityWarpVerifier, new()
    {
        #region Nested Types

        /// <summary>
        /// Represents the handler delegate type of custom code-generation handlers.
        /// </summary>
        /// <param name="backend">The current backend.</param>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="value">The value to generate code for.</param>
        public delegate void Handler(
            VelocityBackend<TILEmitter, TVerifier> backend,
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
        public VelocityBackend(
            Context context,
            CapabilityContext capabilities,
            int warpSize,
            VelocityArgumentMapper argumentMapper)
            : base(
                context,
                capabilities,
                BackendType.Velocity,
                argumentMapper)
        {
            WarpSize = warpSize;
            Instructions = new VelocityInstructions();
            TypeGenerator = new VelocityTypeGenerator(context.RuntimeSystem, warpSize);

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
                        Context.Properties.EnableIOOperations),
                    context.Properties.InliningMode,
                    context.Properties.OptimizationLevel);
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
        /// Returns the current instructions map.
        /// </summary>
        internal VelocityInstructions Instructions { get; }

        /// <summary>
        /// Returns the current type generator.
        /// </summary>
        internal VelocityTypeGenerator TypeGenerator { get; }

        /// <summary>
        /// Returns the associated <see cref="ArgumentMapper"/>.
        /// </summary>
        public new VelocityArgumentMapper ArgumentMapper =>
            base.ArgumentMapper as VelocityArgumentMapper;

        #endregion

        protected override object CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out VelocityCodeGenerator<TILEmitter, TVerifier>.GeneratorArgs data)
        {
            // Create a new generation module
            var module = new VelocityGenerationModule(
                Context.RuntimeSystem,
                Instructions,
                TypeGenerator,
                backendContext,
                entryPoint);
            data = new VelocityCodeGenerator<TILEmitter, TVerifier>.GeneratorArgs(
                Instructions,
                module,
                WarpSize,
                entryPoint);
            return null;
        }

        protected override VelocityCodeGenerator<TILEmitter, TVerifier>
            CreateFunctionCodeGenerator(
            Method method,
            Allocas allocas,
            VelocityCodeGenerator<TILEmitter, TVerifier>.GeneratorArgs data) =>
            new VelocityFunctionGenerator<TILEmitter, TVerifier>(data, method, allocas);

        protected override VelocityCodeGenerator<TILEmitter, TVerifier>
            CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Allocas allocas,
            VelocityCodeGenerator<TILEmitter, TVerifier>.GeneratorArgs data) =>
            new VelocityKernelFunctionGenerator<TILEmitter, TVerifier>(
                data,
                method,
                allocas);

        protected override CompiledKernel CreateKernel(
            EntryPoint entryPoint,
            CompiledKernel.KernelInfo kernelInfo,
            object builder,
            VelocityCodeGenerator<TILEmitter, TVerifier>.GeneratorArgs data)
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

    sealed class VelocityBackend<TILEmitter> :
        VelocityBackend<TILEmitter, VelocityWarpVerifier.Disabled>
        where TILEmitter : struct, IILEmitter
    {
        #region Instance

        /// <summary>
        /// Constructs a new Velocity backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="warpSize">The current warp size.</param>
        /// <param name="argumentMapper">The argument mapper to use.</param>
        public VelocityBackend(
            Context context,
            CapabilityContext capabilities,
            int warpSize,
            VelocityArgumentMapper argumentMapper)
            : base(
                context,
                capabilities,
                warpSize,
                argumentMapper)
        { }

        #endregion
    }
}
