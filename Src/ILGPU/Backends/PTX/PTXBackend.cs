// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX.Transformations;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Transformations;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Specifies which PTX backend-specific features should be used.
    /// </summary>
    public enum PTXBackendMode
    {
        /// <summary>
        /// Enforces the use of the default PTX backend features.
        /// </summary>
        Default,

        /// <summary>
        /// Enables the use of enhanced PTX backend features to improve
        /// performance of the kernel programs being generated.
        /// </summary>
        Enhanced
    }

    /// <summary>
    /// Represents a PTX (Cuda) backend.
    /// </summary>
    public sealed class PTXBackend :
        CodeGeneratorBackend<
            PTXIntrinsic.Handler,
            PTXCodeGenerator.GeneratorArgs,
            PTXCodeGenerator,
            PTXAssembly.Builder>
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

        /// <summary>
        /// Returns the default shared memory alignment in bytes to benefit from
        /// vectorized IO operations in most cases.
        /// </summary>
        public const int DefaultSharedMemoryAlignment = 4;

        #endregion

        #region Nested Types

        /// <summary>
        /// An enumerator for LibDevice backend methods.
        /// </summary>
        public struct LibDeviceEnumerator : IEnumerator<string>
        {
            #region Instance

            private References.Enumerator enumerator;

            /// <summary>
            /// Constructs a new enumerator.
            /// </summary>
            /// <param name="context">The current backend context.</param>
            internal LibDeviceEnumerator(in BackendContext context)
            {
                enumerator = context.Methods.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current value.
            /// </summary>
            public string Current
            {
                get
                {
                    var method = enumerator.Current;
                    return method.Source.Name;
                }
            }

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    if (!current.HasSource)
                        continue;
                    if (current.Source.DeclaringType != typeof(PTXLibDeviceMethods))
                        continue;
                    return true;
                }
                return false;
            }

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new Cuda backend using an implicitly given capability context
        /// that is derived from the specified architecture.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="architecture">The target GPU architecture.</param>
        /// <param name="instructionSet">The target GPU instruction set.</param>
        /// <param name="nvvmAPI">Optional NVVM API instance.</param>
        public PTXBackend(
            Context context,
            CudaArchitecture architecture,
            CudaInstructionSet instructionSet,
            NvvmAPI nvvmAPI)
            : this(
                context,
                new CudaCapabilityContext(architecture),
                architecture,
                instructionSet,
                nvvmAPI)
        { }

        /// <summary>
        /// Constructs a new Cuda backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        /// <param name="capabilities">The supported capabilities.</param>
        /// <param name="architecture">The target GPU architecture.</param>
        /// <param name="instructionSet">The target GPU instruction set.</param>
        /// <param name="nvvmAPI">Optional NVVM API instance.</param>
        public PTXBackend(
            Context context,
            CudaCapabilityContext capabilities,
            CudaArchitecture architecture,
            CudaInstructionSet instructionSet,
            NvvmAPI? nvvmAPI)
            : base(
                  context,
                  capabilities,
                  BackendType.PTX,
                  new PTXArgumentMapper(context))
        {
            Architecture = architecture;
            InstructionSet = instructionSet;
            NvvmAPI = nvvmAPI;
            IsLibDeviceAvailable =
                NvvmAPI != null ||
                Architecture >= PTXLibDevicePtx.MinArchtecture &&
                InstructionSet >= PTXLibDevicePtx.MinInstructionSet;

            InitIntrinsicProvider();
            InitializeKernelTransformers(builder =>
            {
                var transformerBuilder = Transformer.CreateBuilder(
                    TransformerConfiguration.Empty);
                transformerBuilder.AddBackendOptimizations<CodePlacement.GroupOperands>(
                    new PTXAcceleratorSpecializer(
                        PointerType,
                        Context.Properties.EnableAssertions,
                        Context.Properties.EnableIOOperations),
                    context.Properties.InliningMode,
                    context.Properties.OptimizationLevel);

                if (Context.Properties.GetPTXBackendMode() == PTXBackendMode.Enhanced)
                {
                    // Create an optimized PTX assembler block schedule
                    transformerBuilder.Add(new PTXBlockScheduling());
                    transformerBuilder.Add(new DeadCodeElimination());
                }

                builder.Add(transformerBuilder.ToTransformer());
            });
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (NvvmAPI != null)
            {
                NvvmAPI.Dispose();
                NvvmAPI = null;
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current architecture.
        /// </summary>
        public CudaArchitecture Architecture { get; }

        /// <summary>
        /// Returns the current instruction set.
        /// </summary>
        public CudaInstructionSet InstructionSet { get; }

        /// <summary>
        /// Returns the associated <see cref="Backend.ArgumentMapper"/>.
        /// </summary>
        public new PTXArgumentMapper ArgumentMapper =>
            base.ArgumentMapper.AsNotNullCast<PTXArgumentMapper>();

        /// <summary>
        /// Returns the supported capabilities.
        /// </summary>
        public new CudaCapabilityContext Capabilities =>
            base.Capabilities.AsNotNullCast<CudaCapabilityContext>();

        /// <summary>
        /// Returns the NVVM API instance (if available).
        /// </summary>
        public NvvmAPI? NvvmAPI { get; private set; }

        /// <summary>
        /// Returns true if LibDevice is available.
        /// </summary>
        internal bool IsLibDeviceAvailable { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new PTX-compatible kernel builder and initializes a
        /// <see cref="PTXCodeGenerator.GeneratorArgs"/> instance.
        /// </summary>
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
        protected override PTXAssembly.Builder CreateKernelBuilder(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            in KernelSpecialization specialization,
            out PTXCodeGenerator.GeneratorArgs data)
        {
            // Ensure that all intrinsics can be generated
            backendContext.EnsureIntrinsicImplementations(IntrinsicProvider);

            var debugSymbolsMode = Context.Properties.DebugSymbolsMode;
            bool useDebugInfo = debugSymbolsMode > DebugSymbolsMode.Kernel;
            PTXDebugInfoGenerator debugInfoGenerator = PTXNoDebugInfoGenerator.Empty;
            if (useDebugInfo)
            {
                debugInfoGenerator =
                    debugSymbolsMode >= DebugSymbolsMode.KernelSourceAnnotations
                    ? new PTXDebugSourceLineInfoGenerator()
                    : new PTXDebugLineInfoGenerator();
            }

            var assemblyBuilder = new PTXAssembly.Builder();
            var builder = assemblyBuilder.KernelBuilder;

            builder.AppendLine("//");
            builder.Append("// Generated by ILGPU v");
            builder.AppendLine(Context.Version);
            builder.AppendLine("//");
            builder.AppendLine();

            builder.Append(".version ");
            builder.AppendLine(InstructionSet.ToString());
            builder.Append(".target ");
            builder.Append(Architecture.ToString().ToLowerInvariant());
            if (useDebugInfo)
                builder.AppendLine(", debug");
            else
                builder.AppendLine();
            builder.Append(".address_size ");
            builder.AppendLine((PointerSize * 8).ToString());
            builder.AppendLine();

            GenerateLibDeviceCode(backendContext, assemblyBuilder);

            // Check whether we are running in the O1 or O2 pipeline
            bool o1Enabled = Context.Properties.OptimizationLevel >= OptimizationLevel.O1;
            bool o2Enabled = Context.Properties.OptimizationLevel > OptimizationLevel.O1;

            // Creates pointer alignment information in the context of O1 or higher
            var alignments = o1Enabled
                ? PointerAlignments.Apply(
                    backendContext.KernelMethod,
                    DefaultGlobalMemoryAlignment)
                : PointerAlignments.AlignmentInfo.Empty;

            // Create detailed uniform information in O2 builds
            var uniforms = o2Enabled
                ? Uniforms.Apply(backendContext.KernelMethod)
                : Uniforms.Info.Empty;

            data = new PTXCodeGenerator.GeneratorArgs(
                this,
                entryPoint,
                Context.Properties,
                debugInfoGenerator,
                alignments,
                uniforms);

            return assemblyBuilder;
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
            CompiledKernel.KernelInfo? kernelInfo,
            PTXAssembly.Builder builder,
            PTXCodeGenerator.GeneratorArgs data)
        {
            data.DebugInfoGenerator.GenerateDebugSections(builder.KernelBuilder);

            return new PTXCompiledKernel(
                Context,
                entryPoint,
                kernelInfo,
                builder.Seal());
        }

        /// <summary>
        /// Generate the PTX code for LibDevice functions.
        /// </summary>
        /// <param name="backendContext">The backend context.</param>
        /// <param name="builder">The kernel builder.</param>
        private void GenerateLibDeviceCode(
            in BackendContext backendContext,
            PTXAssembly.Builder builder)
        {
            if (backendContext.Count == 0)
                return;

            using var enumerator = new LibDeviceEnumerator(backendContext);
            if (IsLibDeviceAvailable)
            {
                if (NvvmAPI != null)
                {
                    PTXLibDevice.GenerateLibDeviceCode(
                        NvvmAPI,
                        Architecture,
                        enumerator.AsEnumerable(),
                        out var ptx);

                    var compiledString =
                        ptx.AsNotNull()
                        .Replace(".version", "//.version", StringComparison.Ordinal)
                        .Replace(".target", "//.target", StringComparison.Ordinal)
                        .Replace(
                            ".address_size",
                            "//.address_size",
                            StringComparison.Ordinal);
                    builder.KernelBuilder.Append(compiledString);
                }
                else
                {
                    // Use the pre-generated LibDevice PTX code.
                    var ptxModules = InlineList<string>.Create(backendContext.Count);
                    PTXLibDevicePtx.GetPtx(
                        enumerator.AsEnumerable(),
                        ref ptxModules,
                        out var ptxDeclarations);

                    builder.AddModule(ptxModules.AsReadOnlySpan());
                    builder.KernelBuilder.AppendLine(ptxDeclarations);
                }
            }
            else if (enumerator.AsEnumerable().FirstOrDefault() != null)
            {
                // Handle any issues if a LibDevice function is used.
                if (Architecture >= PTXLibDevicePtx.MinArchtecture)
                {
                    // The architecture is supported, but is using an older instruction
                    // set. Can be solved by a driver update.
                    throw new NotSupportedException(string.Format(
                        RuntimeErrorMessages.NotSupportedLibDevicePreGeneratedNewer,
                        PTXLibDevicePtx.MinDriverVersion.Major,
                        PTXLibDevicePtx.MinDriverVersion.Minor));
                }
                else
                {
                    // The architecture is too old for the pre-generated LibDevice PTX.
                    // Inform the user to manually initialize LibDevice.
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedLibDeviceNotInitialized);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for context specific objects.
    /// </summary>
    public static class PTXContextExtensions
    {
        /// <summary>
        /// Specifies a <see cref="PTXBackendMode"/> (will default to
        /// <see cref="PTXBackendMode.Default"/> if not specified).
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="mode">The backend mode to use.</param>
        /// <returns>The current builder instance.</returns>
        public static Context.Builder PTXBackend(
            this Context.Builder builder,
            PTXBackendMode mode)
        {
            builder.SetExtensionProperty(nameof(PTXBackendMode), mode);
            return builder;
        }

        /// <summary>
        /// Gets the current <see cref="PTXBackendMode"/>.
        /// </summary>
        /// <param name="properties">The current properties instance.</param>
        /// <returns>The current PTX backend.</returns>
        public static PTXBackendMode GetPTXBackendMode(
            this ContextProperties properties) =>
            properties.GetExtensionProperty(
                nameof(PTXBackendMode),
                properties.OptimizationLevel > OptimizationLevel.O1
                ? PTXBackendMode.Enhanced
                : PTXBackendMode.Default);

        /// <summary>
        /// Convenience method to get an IEnumerable from an IEnumerator.
        /// </summary>
        public static IEnumerable<T> AsEnumerable<T>(
            this IEnumerator<T> enumerator)
        {
            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }
}
