// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CLBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Transformations;
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
            public bool EnableAssertions => false;
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
                new IntrinsicSpecializerConfiguration(context.Flags),
                builder => { });
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
        public new CLArgumentMapper ArgumentMapper => base.ArgumentMapper as CLArgumentMapper;

        #endregion

        #region Methods

        /// <summary cref="Backend.CreateEntryPoint(in EntryPointDescription, in BackendContext, in KernelSpecialization)"/>
        protected override EntryPoint CreateEntryPoint(
            in EntryPointDescription entry,
            in BackendContext backendContext,
            in KernelSpecialization specialization) =>
            new SeparateViewEntryPoint(
                entry,
                backendContext.SharedMemorySpecification,
                specialization,
                Context.TypeContext);

        /// <summary cref="CodeGeneratorBackend{TDelegate, T, TCodeGenerator, TKernelBuilder}.CreateKernelBuilder(EntryPoint, in BackendContext, in KernelSpecialization, out T)"/>
        protected override StringBuilder CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out CLCodeGenerator.GeneratorArgs data)
        {
            // Ensure that all intrinsics can be generated
            backendContext.EnsureIntrinsicImplementations(IntrinsicProvider);

            var builder = new StringBuilder();
            var typeGenerator = new CLTypeGenerator(Context.TypeContext, ABI.TargetPlatform);

            data = new CLCodeGenerator.GeneratorArgs(
                this,
                typeGenerator,
                entryPoint as SeparateViewEntryPoint,
                ABI);
            return builder;
        }

        /// <summary cref="CodeGeneratorBackend{TDelegate, T, TCodeGenerator, TKernelBuilder}.CreateFunctionCodeGenerator(Method, Scope, Allocas, T)"/>
        protected override CLCodeGenerator CreateFunctionCodeGenerator(
            Method method,
            Scope scope,
            Allocas allocas,
            CLCodeGenerator.GeneratorArgs data) =>
            new CLFunctionGenerator(data, scope, allocas);

        /// <summary cref="CodeGeneratorBackend{TDelegate, T, TCodeGenerator, TKernelBuilder}.CreateKernelCodeGenerator(in AllocaKindInformation, Method, Scope, Allocas, T)"/>
        protected override CLCodeGenerator CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Scope scope,
            Allocas allocas,
            CLCodeGenerator.GeneratorArgs data) =>
            new CLKernelFunctionGenerator(data, scope, allocas);

        /// <summary cref="CodeGeneratorBackend{TDelegate, T, TCodeGenerator, TKernelBuilder}.CreateKernel(EntryPoint, TKernelBuilder, T)"/>
        protected override CompiledKernel CreateKernel(
            EntryPoint entryPoint,
            StringBuilder builder,
            CLCodeGenerator.GeneratorArgs data)
        {
            var typeBuilder = new StringBuilder();
            data.TypeGenerator.GenerateTypeDeclarations(typeBuilder);
            data.TypeGenerator.GenerateTypeDefinitions(typeBuilder);

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
