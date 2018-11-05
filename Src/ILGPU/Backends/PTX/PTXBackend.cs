// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Construction;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents a PTX (Cuda) backend.
    /// </summary>
    public sealed class PTXBackend : Backend
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
        private readonly struct SpecializerConfiguration : IKernelSpecializerConfiguration
        {
            /// <summary>
            /// Constructs a new specializer configuration.
            /// </summary>
            /// <param name="flags">The associated context flags.</param>
            /// <param name="abi">The ABI specification.</param>
            /// <param name="contextData">The global PTX context data.</param>
            /// <param name="specializer">The import specializer.</param>
            public SpecializerConfiguration(
                IRContextFlags flags,
                ABI abi,
                PTXContextData contextData,
                in ContextImportSpecification.Specializer specializer)
            {
                ContextFlags = flags;
                ABI = abi;
                ImplementationResolver = contextData.ImplementationResolver;
                Specializer = specializer;
            }

            /// <summary>
            /// Returns the associated context.
            /// </summary>
            public IRContextFlags ContextFlags { get; }

            /// <summary cref="IKernelSpecializerConfiguration.EnableAssertions"/>
            public bool EnableAssertions => (ContextFlags & IRContextFlags.EnableAssertions) == IRContextFlags.EnableAssertions;

            /// <summary cref="IKernelSpecializerConfiguration.WarpSize"/>
            public int WarpSize => PTXBackend.WarpSize;

            /// <summary>
            /// Returns the associated specializer.
            /// </summary>
            public ContextImportSpecification.Specializer Specializer { get; }

            /// <summary>
            /// Returns the current ABI.
            /// </summary>
            public ABI ABI { get; }

            /// <summary cref="IIntrinsicSpecializerConfiguration.ImplementationResolver"/>
            public IntrinsicImplementationResolver ImplementationResolver { get; }

            /// <summary cref="IKernelSpecializerConfiguration.SpecializeKernelParameter(IRBuilder, FunctionBuilder, Parameter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Value SpecializeKernelParameter(
                IRBuilder builder,
                FunctionBuilder functionBuilder,
                Parameter parameter)
            {
                var paramType = parameter.Type;
                if (!builder.TrySpecializeAddressSpaceType(
                    paramType,
                    MemoryAddressSpace.Global,
                    out TypeNode specializedType))
                    return null;
                var targetParam = functionBuilder.AddParameter(specializedType, parameter.Name);
                var addressSpaceCast = builder.CreateAddressSpaceCast(
                    targetParam,
                    MemoryAddressSpace.Generic);
                return addressSpaceCast;
            }

            /// <summary cref="ISizeOfABI.GetSizeOf(TypeNode)" />
            public int GetSizeOf(TypeNode type) => ABI.GetSizeOf(type);

            /// <summary cref="IIntrinsicSpecializerConfiguration.TryGetSizeOf(TypeNode, out int)"/>
            public bool TryGetSizeOf(TypeNode type, out int size)
            {
                size = GetSizeOf(type);
                return true;
            }

            /// <summary cref="IFunctionImportSpecializer.Map(IRContext, TopLevelFunction, IRBuilder, IRRebuilder)"/>
            public void Map(
                IRContext sourceContext,
                TopLevelFunction sourceFunction,
                IRBuilder builder,
                IRRebuilder rebuilder) =>
                Specializer.Map(sourceContext, sourceFunction, builder, rebuilder);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new Cuda backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="architecture">The target gpu architecture.</param>
        /// <param name="platform">The target platform.</param>
        public PTXBackend(
            Context context,
            PTXArchitecture architecture,
            TargetPlatform platform)
            : base(context, platform, new PTXArgumentMapper(context),
                  (builder, maxNumIterations) =>
                  {
                      builder.Add(new DestroyStructures(), maxNumIterations);
                      builder.Add(new NormalizeCalls(), 1);
                  })
        {
            Architecture = architecture;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current architecture.
        /// </summary>
        public PTXArchitecture Architecture { get; }

        /// <summary>
        /// Returns the associated <see cref="KernelArgumentMapper"/>.
        /// </summary>
        public new PTXArgumentMapper KernelArgumentMapper =>
            base.KernelArgumentMapper as PTXArgumentMapper;

        #endregion

        #region Methods

        /// <summary cref="Backend.CreateImportSpecification"/>
        protected override ContextImportSpecification CreateImportSpecification() =>
            new ContextImportSpecification();

        /// <summary cref="Backend.PrepareKernel(IRContext, TopLevelFunction, ABI, in ContextImportSpecification)"/>
        protected override void PrepareKernel(
            IRContext kernelContext,
            TopLevelFunction kernelFunction,
            ABI abi,
            in ContextImportSpecification importSpecification)
        {
            var configuration = new SpecializerConfiguration(
                kernelContext.Flags,
                abi,
                Context.PTXContextData,
                importSpecification.ToSpecializer());
            var kernelSpecializer = new KernelSpecializer<SpecializerConfiguration>(
                configuration);
            kernelSpecializer.PrepareKernel(
                ref kernelFunction,
                kernelContext);

            var transformer = SpecializeViews.CreateTransformer(
                TransformerConfiguration.Empty,
                PointerViewImplSpecializer.Create(abi));
            transformer.Transform(kernelContext);
        }

        /// <summary>
        /// Initializes a PTX kernel and returns the created <see cref="StringBuilder"/>.
        /// </summary>
        /// <param name="abi">The current ABI.</param>
        /// <param name="constantOffset">The offset for constants.</param>
        /// <returns>The created string builder.</returns>
        private StringBuilder CreatePTXBuilder(
            ABI abi,
            out int constantOffset)
        {
            var builder = new StringBuilder();

            builder.AppendLine("//");
            builder.Append("// Generated by ILGPU v");
            builder.AppendLine(Context.Version);
            builder.AppendLine("//");
            builder.AppendLine();

            builder.Append(".version ");
            builder.AppendLine(PTXCodeGenerator.PTXVersion);
            builder.Append(".target ");
            builder.AppendLine(Architecture.ToString().ToLower());
            builder.Append(".address_size ");
            builder.AppendLine((abi.PointerSize * 8).ToString());
            builder.AppendLine();

            constantOffset = builder.Length;

            return builder;
        }

        /// <summary cref="Backend.Compile(EntryPoint, ABI, in BackendContext, in KernelSpecialization)"/>
        protected override CompiledKernel Compile(
            EntryPoint entryPoint,
            ABI abi,
            in BackendContext backendContext,
            in KernelSpecialization specialization)
        {
            var builder = CreatePTXBuilder(abi, out int constantOffset);
            var useFastMath = (Context.Flags & IRContextFlags.FastMath) == IRContextFlags.FastMath;
            var enableAssertions = (Context.Flags & IRContextFlags.EnableAssertions) == IRContextFlags.EnableAssertions;

            var args = new PTXCodeGenerator.GeneratorArgs(
                entryPoint,
                builder,
                Architecture,
                abi,
                useFastMath,
                enableAssertions);

            foreach (var entry in backendContext)
            {
                PTXFunctionGenerator.Generate(
                    args,
                    entry.Scope,
                    entry.Data.Allocas,
                    ref constantOffset);
            }

            var kernelFunction = backendContext.KernelFunction;
            PTXKernelFunctionGenerator.Generate(
                args,
                kernelFunction.Scope,
                kernelFunction.Data.Allocas,
                backendContext.SharedAllocations,
                ref constantOffset);


            var ptxAssembly = builder.ToString();
            return new PTXCompiledKernel(Context, entryPoint, ptxAssembly);
        }

        #endregion
    }
}
