﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.Runtime;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a PTX (Cuda) backend.
    /// </summary>
    public sealed class PTXBackend :
        CodeGeneratorBackend<
            PTXIntrinsic.Handler,
            PTXCodeGenerator.GeneratorArgs,
            PTXCodeGenerator,
            StringBuilder>
    {
        #region Constants

        /// <summary>
        /// Returns the warp size.
        /// </summary>
        public const int WarpSize = 32;

        /// <summary>
        /// Returns the default global memory alignment in bytes.
        /// </summary>
        /// <remarks>
        /// See Cuda documentation section 5.3.2.
        /// </remarks>
        public const int DefaultGlobalMemoryAlignment = 256;

        #endregion

        #region Nested Types

        /// <summary>
        /// The PTX accelerator specializer.
        /// </summary>
        private sealed class PTXAcceleratorSpecializer : AcceleratorSpecializer
        {
            public PTXAcceleratorSpecializer(PrimitiveType pointerType)
                : base(
                      AcceleratorType.Cuda,
                      PTXBackend.WarpSize,
                      pointerType)
            { }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new Cuda backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="architecture">The target GPU architecture.</param>
        /// <param name="instructionSet">The target GPU instruction set.</param>
        public PTXBackend(
            Context context,
            PTXArchitecture architecture,
            PTXInstructionSet instructionSet)
            : base(
                  context,
                  BackendType.PTX,
                  BackendFlags.None,
                  new PTXArgumentMapper(context))
        {
            Architecture = architecture;
            InstructionSet = instructionSet;

            InitIntrinsicProvider();
            InitializeKernelTransformers(
                Context.HasFlags(ContextFlags.EnableAssertions) ?
                IntrinsicSpecializerFlags.EnableAssertions :
                IntrinsicSpecializerFlags.None,
                builder =>
            {
                var transformerBuilder = Transformer.CreateBuilder(
                    TransformerConfiguration.Empty);
                transformerBuilder.AddBackendOptimizations(
                    new PTXAcceleratorSpecializer(PointerType),
                    context.OptimizationLevel);
                builder.Add(transformerBuilder.ToTransformer());
            });
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current architecture.
        /// </summary>
        public PTXArchitecture Architecture { get; }

        /// <summary>
        /// Returns the current instruction set.
        /// </summary>
        public PTXInstructionSet InstructionSet { get; }

        /// <summary>
        /// Returns the associated <see cref="Backend.ArgumentMapper"/>.
        /// </summary>
        public new PTXArgumentMapper ArgumentMapper =>
            base.ArgumentMapper as PTXArgumentMapper;

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new PTX-compatible kernel builder and initializes a
        /// <see cref="PTXCodeGenerator.GeneratorArgs"/> instance.
        /// </summary>
        protected override StringBuilder CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out PTXCodeGenerator.GeneratorArgs data)
        {
            // Ensure that all intrinsics can be generated
            backendContext.EnsureIntrinsicImplementations(IntrinsicProvider);

            bool useDebugInfo = Context.HasFlags(
                ContextFlags.EnableKernelDebugInformation);
            PTXDebugInfoGenerator debugInfoGenerator = PTXNoDebugInfoGenerator.Empty;
            if (useDebugInfo)
            {
                debugInfoGenerator =
                    Context.HasFlags(ContextFlags.EnableInlineSourceAnnotations)
                    ? new PTXDebugSourceLineInfoGenerator()
                    : new PTXDebugLineInfoGenerator();
            }

            var builder = new StringBuilder();

            builder.AppendLine("//");
            builder.Append("// Generated by ILGPU v");
            builder.AppendLine(Context.Version);
            builder.AppendLine("//");
            builder.AppendLine();

            builder.Append(".version ");
            builder.AppendLine(InstructionSet.ToString());
            builder.Append(".target ");
            builder.Append(Architecture.ToString().ToLower());
            if (useDebugInfo)
                builder.AppendLine(", debug");
            else
                builder.AppendLine();
            builder.Append(".address_size ");
            builder.AppendLine((PointerSize * 8).ToString());
            builder.AppendLine();

            // Creates pointer alignment information in the context of O2 or higher
            var alignments = Context.OptimizationLevel >= OptimizationLevel.O2
                ? PointerAlignments.Create(
                    backendContext.KernelMethod,
                    DefaultGlobalMemoryAlignment)
                : null;

            data = new PTXCodeGenerator.GeneratorArgs(
                this,
                entryPoint,
                Context.Flags,
                debugInfoGenerator,
                alignments);

            return builder;
        }

        /// <summary>
        /// Creates a new <see cref="PTXFunctionGenerator"/>.
        /// </summary>
        protected override PTXCodeGenerator CreateFunctionCodeGenerator(
            Method method,
            Allocas allocas,
            PTXCodeGenerator.GeneratorArgs data) =>
            new PTXFunctionGenerator(data, method, allocas);

        /// <summary>
        /// Creates a new <see cref="PTXFunctionGenerator"/>.
        /// </summary>
        protected override PTXCodeGenerator CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Allocas allocas,
            PTXCodeGenerator.GeneratorArgs data) =>
            new PTXKernelFunctionGenerator(data, method, allocas);

        /// <summary>
        /// Creates a new <see cref="PTXCompiledKernel"/> and initializes all debug
        /// information sections.
        /// </summary>
        protected override CompiledKernel CreateKernel(
            EntryPoint entryPoint,
            CompiledKernel.KernelInfo kernelInfo,
            StringBuilder builder,
            PTXCodeGenerator.GeneratorArgs data)
        {
            data.DebugInfoGenerator.GenerateDebugSections(builder);

            var ptxAssembly = builder.ToString();
            return new PTXCompiledKernel(
                Context,
                entryPoint,
                kernelInfo,
                ptxAssembly);
        }

        #endregion
    }
}
