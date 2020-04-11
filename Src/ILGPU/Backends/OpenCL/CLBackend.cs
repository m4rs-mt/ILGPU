// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLBackend.cs
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
using ILGPU.Runtime.OpenCL;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents an OpenCL source backend.
    /// </summary>
    public sealed partial class CLBackend :
        CodeGeneratorBackend<
            CLIntrinsic.Handler,
            CLCodeGenerator.GeneratorArgs,
            CLCodeGenerator,
            StringBuilder>
    {
        #region Nested Types

        /// <summary>
        /// The OpenCL accelerator specializer.
        /// </summary>
        private sealed class CLAcceleratorSpecializer : AcceleratorSpecializer
        {
            public CLAcceleratorSpecializer(ABI abi)
                : base(AcceleratorType.OpenCL, null)
            {
                ABI = abi;
            }

            /// <summary>
            /// Returns the current ABI.
            /// </summary>
            public ABI ABI { get; }

            /// <summary>
            /// Resolves the size of the given type node.
            /// </summary>
            protected override bool TryGetSizeOf(TypeNode type, out int size)
            {
                size = ABI.GetSizeOf(type);
                return true;
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// Represents the minimum OpenCL C version that is required.
        /// </summary>
        public static readonly CLCVersion MinimumVersion = new CLCVersion(2, 0);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new OpenCL source backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="platform">The target platform.</param>
        /// <param name="vendor">The associated major vendor.</param>
        public CLBackend(
            Context context,
            TargetPlatform platform,
            CLAcceleratorVendor vendor)
            : base(
                  context,
                  BackendType.OpenCL,
                  BackendFlags.None,
                  new CLABI(context.TypeContext, platform),
                  abi => new CLArgumentMapper(context, abi))
        {
            Vendor = vendor;

            InitializeKernelTransformers(
                IntrinsicSpecializerFlags.None,
                builder =>
                {
                    var transformerBuilder = Transformer.CreateBuilder(
                        TransformerConfiguration.Empty);
                    transformerBuilder.AddBackendOptimizations(
                        new CLAcceleratorSpecializer(ABI),
                        context.OptimizationLevel);
                    builder.Add(transformerBuilder.ToTransformer());
                });
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated major accelerator vendor.
        /// </summary>
        public CLAcceleratorVendor Vendor { get; }

        /// <summary>
        /// Returns the associated <see cref="Backend.ArgumentMapper"/>.
        /// </summary>
        public new CLArgumentMapper ArgumentMapper =>
            base.ArgumentMapper as CLArgumentMapper;

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new <see cref="SeparateViewEntryPoint"/> instance.
        /// </summary>
        protected override EntryPoint CreateEntryPoint(
            in EntryPointDescription entry,
            in BackendContext backendContext,
            in KernelSpecialization specialization) =>
            new SeparateViewEntryPoint(
                entry,
                backendContext.SharedMemorySpecification,
                specialization,
                Context.TypeContext,
                2);

        /// <summary>
        /// Creates a new <see cref="StringBuilder"/> and configures a
        /// <see cref="CLCodeGenerator.GeneratorArgs"/> instance.
        /// </summary>
        protected override StringBuilder CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out CLCodeGenerator.GeneratorArgs data)
        {
            // Ensure that all intrinsics can be generated
            backendContext.EnsureIntrinsicImplementations(IntrinsicProvider);

            var builder = new StringBuilder();
            var typeGenerator = new CLTypeGenerator(Context.TypeContext);

            data = new CLCodeGenerator.GeneratorArgs(
                this,
                typeGenerator,
                entryPoint as SeparateViewEntryPoint,
                ABI);
            return builder;
        }

        /// <summary>
        /// Creates a new <see cref="CLFunctionGenerator"/>.
        /// </summary>
        protected override CLCodeGenerator CreateFunctionCodeGenerator(
            Method method,
            Scope scope,
            Allocas allocas,
            CLCodeGenerator.GeneratorArgs data) =>
            new CLFunctionGenerator(data, scope, allocas);

        /// <summary>
        /// Generates a new <see cref="CLKernelFunctionGenerator"/>.
        /// </summary>
        protected override CLCodeGenerator CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Scope scope,
            Allocas allocas,
            CLCodeGenerator.GeneratorArgs data) =>
            new CLKernelFunctionGenerator(data, scope, allocas);

        /// <summary>
        /// Creates a new <see cref="CLCompiledKernel"/>.
        /// </summary>
        protected override CompiledKernel CreateKernel(
            EntryPoint entryPoint,
            StringBuilder builder,
            CLCodeGenerator.GeneratorArgs data)
        {
            var typeBuilder = new StringBuilder();
            data.TypeGenerator.GenerateTypeDeclarations(typeBuilder);
            data.KernelTypeGenerator.GenerateTypeDeclarations(typeBuilder);

            data.TypeGenerator.GenerateTypeDefinitions(typeBuilder);
            data.KernelTypeGenerator.GenerateTypeDefinitions(typeBuilder);

            builder.Insert(0, typeBuilder.ToString());

            var clSource = builder.ToString();
            return new CLCompiledKernel(
                Context,
                entryPoint as SeparateViewEntryPoint,
                clSource,
                MinimumVersion);
        }

        #endregion
    }
}
