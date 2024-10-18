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
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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

        /// <summary>
        /// Returns the default shared memory alignment in bytes to benefit from
        /// vectorized IO operations in most cases.
        /// </summary>
        public const int DefaultSharedMemoryAlignment = 4;

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

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new PTX-compatible kernel builder and initializes a
        /// <see cref="PTXCodeGenerator.GeneratorArgs"/> instance.
        /// </summary>
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
        protected override StringBuilder CreateKernelBuilder(
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

            var builder = new StringBuilder();

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

            GenerateLibDeviceCode(backendContext, builder);

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
            CompiledKernel.KernelInfo? kernelInfo,
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

        private unsafe void GenerateLibDeviceCode(
            in BackendContext backendContext,
            StringBuilder builder)
        {
            if (NvvmAPI == null || backendContext.Count == 0)
                return;

            // Determine the NVVM IR Version to use.
            var result = NvvmAPI.GetIRVersion(out int majorIR, out _, out _, out _);
            if (result != NvvmResult.NVVM_SUCCESS)
                return;

            // Convert the methods in the context into NVVM.
            var methods = backendContext.GetEnumerator().AsEnumerable();
            var nvvmModule = PTXLibDeviceNvvm.GenerateNvvm(majorIR, methods);

            if (string.IsNullOrEmpty(nvvmModule))
                return;

            // Create a new NVVM program.
            result = NvvmAPI.CreateProgram(out var program);

            try
            {
                // Add custom NVVM module.
                if (result == NvvmResult.NVVM_SUCCESS)
                {
                    var nvvmModuleBytes = Encoding.ASCII.GetBytes(nvvmModule);
                    fixed (byte* nvvmPtr = nvvmModuleBytes)
                    {
                        result = NvvmAPI.AddModuleToProgram(
                            program,
                            new IntPtr(nvvmPtr),
                            new IntPtr(nvvmModuleBytes.Length),
                            null);
                    }
                }

                // Add the LibDevice bit code.
                if (result == NvvmResult.NVVM_SUCCESS)
                {
                    fixed (byte* ptr = NvvmAPI.LibDeviceBytes)
                    {
                        result = NvvmAPI.LazyAddModuleToProgram(
                            program,
                            new IntPtr(ptr),
                            new IntPtr(NvvmAPI.LibDeviceBytes.Length),
                            null);
                    }
                }

                // Compile the NVVM into PTX for the backend architecture.
                if (result == NvvmResult.NVVM_SUCCESS)
                {
                    var major = Architecture.Major;
                    var minor = Architecture.Minor;
                    var archOption = $"-arch=compute_{major}{minor}";
                    var archOptionAscii = Encoding.ASCII.GetBytes(archOption);
                    fixed (byte* archOptionPtr = archOptionAscii)
                    {
                        var numOptions = 1;
                        var optionValues = stackalloc byte[sizeof(void*) * numOptions];
                        var values = (void**)optionValues;
                        values[0] = archOptionPtr;

                        result = NvvmAPI.CompileProgram(
                            program,
                            numOptions,
                            new IntPtr(values));
                    }
                }

                // Extract the PTX result and comment out the initial declarations.
                if (result == NvvmResult.NVVM_SUCCESS)
                {
                    result = NvvmAPI.GetCompiledResult(program, out var compiledPTX);
                    if (result == NvvmResult.NVVM_SUCCESS)
                    {
                        var compiledString =
                            compiledPTX.AsNotNull()
                            .Replace(".version", "//.version", StringComparison.Ordinal)
                            .Replace(".target", "//.target", StringComparison.Ordinal)
                            .Replace(
                                ".address_size",
                                "//.address_size",
                                StringComparison.Ordinal);
                        builder.Append(compiledString);
                    }
                }
            }
            finally
            {
                NvvmAPI.DestroyProgram(ref program);
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
        /// Convenience method to get an IEnumerable of Method.
        /// </summary>
        public static IEnumerable<Method> AsEnumerable(
            this IEnumerator<(Method, Allocas)> enumerator)
        {
            while (enumerator.MoveNext())
                yield return enumerator.Current.Item1;
        }
    }
}
