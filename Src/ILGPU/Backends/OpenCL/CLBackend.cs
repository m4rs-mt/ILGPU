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
using ILGPU.Backends.OpenCL.Transformations;
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
    public sealed class CLBackend :
        CodeGeneratorBackend<
            CLIntrinsic.Handler,
            CLCodeGenerator.GeneratorArgs,
            CLCodeGenerator,
            StringBuilder>
    {
        #region Static

        /// <summary>
        /// Represents the minimum OpenCL C version that is required.
        /// </summary>
        public static readonly CLCVersion MinimumVersion = new CLCVersion(2, 0);

        #endregion

        #region Instance

        /// <summary>
        /// Returns the list of enabled OpenCL extensions.
        /// </summary>
        private readonly string extensions;

        /// <summary>
        /// Constructs a new OpenCL source backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="vendor">The associated major vendor.</param>
        public CLBackend(
            Context context,
            CLCapabilityContext capabilities,
            CLAcceleratorVendor vendor)
            : base(
                  context,
                  capabilities,
                  BackendType.OpenCL,
                  BackendFlags.None,
                  new CLArgumentMapper(context))
        {
            Vendor = vendor;

            InitIntrinsicProvider();
            InitializeKernelTransformers(
                IntrinsicSpecializerFlags.None,
                builder =>
                {
                    var transformerBuilder = Transformer.CreateBuilder(
                        TransformerConfiguration.Empty);
                    transformerBuilder.AddBackendOptimizations(
                        new CLAcceleratorSpecializer(PointerType),
                        context.OptimizationLevel);
                    builder.Add(transformerBuilder.ToTransformer());
                });

            // Build a list of extensions to enable for each OpenCL kernel.
            var extensionBuilder = new StringBuilder();
            foreach (var extensionName in Capabilities.Extensions)
            {
                extensionBuilder.Append("#pragma OPENCL EXTENSION ");
                extensionBuilder.Append(extensionName);
                extensionBuilder.AppendLine(" : enable");
            }
            extensions = extensionBuilder.ToString();
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

        /// <summary>
        /// Returns the capabilities of this accelerator.
        /// </summary>
        public new CLCapabilityContext Capabilities =>
            base.Capabilities as CLCapabilityContext;

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

            builder.AppendLine("//");
            builder.Append("// Generated by ILGPU v");
            builder.AppendLine(Context.Version);
            builder.AppendLine("//");
            builder.AppendLine(extensions);

            var typeGenerator = new CLTypeGenerator(Context.TypeContext, Capabilities);

            data = new CLCodeGenerator.GeneratorArgs(
                this,
                typeGenerator,
                entryPoint as SeparateViewEntryPoint,
                backendContext.SharedAllocations,
                backendContext.DynamicSharedAllocations);
            return builder;
        }

        /// <summary>
        /// Creates a new <see cref="CLFunctionGenerator"/>.
        /// </summary>
        protected override CLCodeGenerator CreateFunctionCodeGenerator(
            Method method,
            Allocas allocas,
            CLCodeGenerator.GeneratorArgs data) =>
            new CLFunctionGenerator(data, method, allocas);

        /// <summary>
        /// Generates a new <see cref="CLKernelFunctionGenerator"/>.
        /// </summary>
        protected override CLCodeGenerator CreateKernelCodeGenerator(
            in AllocaKindInformation sharedAllocations,
            Method method,
            Allocas allocas,
            CLCodeGenerator.GeneratorArgs data) =>
            new CLKernelFunctionGenerator(data, method, allocas);

        /// <summary>
        /// Creates a new <see cref="CLCompiledKernel"/>.
        /// </summary>
        protected override CompiledKernel CreateKernel(
            EntryPoint entryPoint,
            CompiledKernel.KernelInfo kernelInfo,
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
                kernelInfo,
                clSource,
                MinimumVersion);
        }

        #endregion
    }
}
