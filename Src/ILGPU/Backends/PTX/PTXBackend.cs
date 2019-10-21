// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.Runtime;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a PTX (Cuda) backend.
    /// </summary>
    public sealed class PTXBackend : Backend<PTXIntrinsic.Handler>
    {
        #region Constants

        /// <summary>
        /// Returns the warp size.
        /// </summary>
        public const int WarpSize = 32;

        #endregion

        #region Nested Types

        /// <summary>
        /// The kernel specializer configuration.
        /// </summary>
        private readonly struct IntrinsicSpecializerConfiguration : IIntrinsicSpecializerConfiguration
        {
            /// <summary>
            /// Constructs a new specializer configuration.
            /// </summary>
            /// <param name="flags">The associated context flags.</param>
            public IntrinsicSpecializerConfiguration(ContextFlags flags)
            {
                ContextFlags = flags;
            }

            /// <summary>
            /// Returns the associated context.
            /// </summary>
            public ContextFlags ContextFlags { get; }

            /// <summary cref="IIntrinsicSpecializerConfiguration.EnableAssertions"/>
            public bool EnableAssertions => ContextFlags.HasFlags(ContextFlags.EnableAssertions);
        }

        /// <summary>
        /// The kernel specializer configuration.
        /// </summary>
        private readonly struct AcceleratorSpecializerConfiguration : IAcceleratorSpecializerConfiguration
        {
            /// <summary>
            /// Constructs a new specializer configuration.
            /// </summary>
            /// <param name="abi">The ABI specification.</param>
            public AcceleratorSpecializerConfiguration(ABI abi)
            {
                ABI = abi;
            }

            /// <summary cref="IAcceleratorSpecializerConfiguration.WarpSize"/>
            public int WarpSize => PTXBackend.WarpSize;

            /// <summary>
            /// Returns the current ABI.
            /// </summary>
            public ABI ABI { get; }

            /// <summary cref="IAcceleratorSpecializerConfiguration.TryGetSizeOf(TypeNode, out int)"/>
            public bool TryGetSizeOf(TypeNode type, out int size)
            {
                size = ABI.GetSizeOf(type);
                return true;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new Cuda backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="architecture">The target gpu architecture.</param>
        /// <param name="instructionSet">The target gpu instruction set.</param>
        /// <param name="platform">The target platform.</param>
        public PTXBackend(
            Context context,
            PTXArchitecture architecture,
            PTXInstructionSet instructionSet,
            TargetPlatform platform)
            : base(
                  context,
                  BackendType.PTX,
                  BackendFlags.RequiresIntrinsicImplementations,
                  new PTXABI(context.TypeContext, platform),
                  new PTXArgumentMapper(context))
        {
            Architecture = architecture;
            InstructionSet = instructionSet;

            InitializeKernelTransformers(
                new IntrinsicSpecializerConfiguration(context.Flags),
                builder =>
            {
                // Append further backend specific transformations in release mode
                var transformerBuilder = Transformer.CreateBuilder(TransformerConfiguration.Empty);

                if (context.OptimizationLevel == OptimizationLevel.Release)
                {
                    var acceleratorConfiguration = new AcceleratorSpecializerConfiguration(ABI);
                    transformerBuilder.Add(
                        new AcceleratorSpecializer<AcceleratorSpecializerConfiguration>(
                            acceleratorConfiguration));
                }

                if (!context.HasFlags(ContextFlags.NoInlining))
                    transformerBuilder.Add(new Inliner());

                if (context.OptimizationLevel == OptimizationLevel.Release)
                    transformerBuilder.Add(new SimplifyControlFlow());

                var transformer = transformerBuilder.ToTransformer();
                if (transformer.Length > 0)
                    builder.Add(transformer);
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
        /// Returns the associated <see cref="ArgumentMapper"/>.
        /// </summary>
        public new PTXArgumentMapper ArgumentMapper => base.ArgumentMapper as PTXArgumentMapper;

        #endregion

        #region Methods

        /// <summary>
        /// Initializes a PTX kernel and returns the created <see cref="StringBuilder"/>.
        /// </summary>
        /// <param name="useDebugInfo">True, if this kernel uses debug information.</param>
        /// <param name="constantOffset">The offset for constants.</param>
        /// <returns>The created string builder.</returns>
        private StringBuilder CreatePTXBuilder(
            bool useDebugInfo,
            out int constantOffset)
        {
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
            builder.AppendLine((ABI.PointerSize * 8).ToString());
            builder.AppendLine();

            constantOffset = builder.Length;

            return builder;
        }

        /// <summary cref="Backend.Compile(EntryPoint, in BackendContext, in KernelSpecialization)"/>
        protected override CompiledKernel Compile(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization)
        {
            bool useDebugInfo = Context.HasFlags(ContextFlags.EnableDebugInformation);

            var builder = CreatePTXBuilder(useDebugInfo, out int constantOffset);
            PTXDebugInfoGenerator debugInfoGenerator = PTXNoDebugInfoGenerator.Empty;
            if (useDebugInfo)
            {
                debugInfoGenerator = Context.HasFlags(ContextFlags.EnableInlineSourceAnnotations) ?
                    new PTXDebugSourceLineInfoGenerator() :
                    new PTXDebugLineInfoGenerator();
            }

            var args = new PTXCodeGenerator.GeneratorArgs(
                this,
                entryPoint,
                debugInfoGenerator,
                builder,
                Context.Flags);

            // Declare all methods
            foreach (var (_, scope, _) in backendContext)
                PTXFunctionGenerator.GenerateHeader(args, scope);

            // Emit methods
            foreach (var (_, scope, allocas) in backendContext)
            {
                PTXFunctionGenerator.Generate(
                    args,
                    scope,
                    allocas,
                    ref constantOffset);
            }

            // Generate kernel method
            PTXKernelFunctionGenerator.Generate(
                args,
                backendContext.KernelScope,
                backendContext.KernelAllocas,
                backendContext.SharedAllocations,
                ref constantOffset);

            // Append final debug information
            debugInfoGenerator.GenerateDebugSections(builder);

            var ptxAssembly = builder.ToString();
            return new PTXCompiledKernel(Context, entryPoint, ptxAssembly);
        }

        #endregion
    }
}
