// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2017-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Backend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPUC.Compilers;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.Transformations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Backends;

// These types will be consumed by external test frameworks
#pragma warning disable CA1515 // Consider making public types internal

/// <summary>
/// Represents the general type of a backend.
/// </summary>
public enum BackendType
{
    /// <summary>
    /// A metal backend.
    /// </summary>
    Metal,

    /// <summary>
    /// An OpenCL backend.
    /// </summary>
    OpenCL,

    /// <summary>
    /// A CUDA backend.
    /// </summary>
    Cuda,

    /// <summary>
    /// A ROCm backend.
    /// </summary>
    ROCm,

    /// <summary>
    /// A CPU backend for vectorized CPU execution.
    /// </summary>
    CPU,
}

/// <summary>
/// Represents a general ILGPU backend.
/// </summary>
/// <remarks>
/// Constructs a new generic backend.
/// </remarks>
/// <param name="backendType">The backend type.</param>
/// <param name="acceleratorType">The accelerator type.</param>
/// <param name="capabilities">The supported capabilities.</param>
abstract class Backend(
    BackendType backendType,
    AcceleratorType acceleratorType,
    AcceleratorCapabilities capabilities)
{
    /// <summary>
    /// Returns the associated backend type.
    /// </summary>
    public BackendType BackendType { get; } = backendType;

    /// <summary>
    /// Returns the associated accelerator type.
    /// </summary>
    public AcceleratorType AcceleratorType { get; } = acceleratorType;

    /// <summary>
    /// Returns the current warp size (if available).
    /// </summary>
    public virtual int? CurrentWarpSize { get; }

    /// <summary>
    /// Returns the supported capabilities.
    /// </summary>
    public AcceleratorCapabilities Capabilities { get; } = capabilities;

    /// <summary>
    /// Returns the underlying language configuration.
    /// </summary>
    public abstract LanguageConfiguration LanguageConfiguration { get; }

    /// <summary>
    /// Creates a platform-specific launcher emitter for this backend.
    /// </summary>
    public abstract LauncherEmitter CreateLauncherEmitter();

    /// <summary>
    /// Creates a platform-specific compiled kernel emitter for this backend.
    /// </summary>
    public abstract CompiledKernelEmitter CreateCompiledKernelEmitter();

    /// <summary>
    /// Creates a new architecture specification used to specialize a module for this
    /// backend.
    /// </summary>
    protected abstract ArchitectureSpecification GetArchitectureSpecification();

    /// <summary>
    /// Creates a backend transformer to transform a given module into the right IR shape.
    /// </summary>
    /// <param name="properties">The compilation properties to use.</param>
    /// <returns>The transformer to be used.</returns>
    public Transformer CreateBackendTransformer(CompilationProperties properties)
    {
        var builder = Transformer.CreateBuilder();

        // Specialize for this accelerator
        builder.AddAcceleratorSpecializer(GetArchitectureSpecification());

        // Add general optimizations
        builder.AddBasicOptimizations();

        return builder.ToTransformer();
    }

    /// <summary>
    /// Generates code for the given module.
    /// </summary>
    /// <param name="properties">Compilation properties to use.</param>
    /// <param name="typeInformationManager">Shared type manager to use.</param>
    /// <param name="module">The module to generate code for.</param>
    /// <returns>The generated code.</returns>
    public CodeGenerationResult GenerateCode(
        CompilationProperties properties,
        TypeInformationManager typeInformationManager,
        Module module) =>
        GenerateCode(properties, typeInformationManager, module, dumpWriter: null);

    /// <summary>
    /// Generates code for the given module, optionally dumping the post-backend-
    /// transform IR to <paramref name="dumpWriter"/>.
    /// </summary>
    /// <param name="properties">Compilation properties to use.</param>
    /// <param name="typeInformationManager">Shared type manager to use.</param>
    /// <param name="module">The module to generate code for.</param>
    /// <param name="dumpWriter">
    /// Optional writer that receives a normalized IR dump after backend-specific
    /// transforms are applied. Pass <see langword="null"/> to skip the dump.
    /// </param>
    /// <param name="dumpFormat">
    /// The IR printer format to use when writing the dump.
    /// Defaults to <see cref="IRPrinterFormat.ILGPU"/>.
    /// </param>
    /// <returns>The generated code.</returns>
    public CodeGenerationResult GenerateCode(
        CompilationProperties properties,
        TypeInformationManager typeInformationManager,
        Module module,
        TextWriter? dumpWriter,
        IRPrinterFormat dumpFormat = IRPrinterFormat.ILGPU)
    {
        var transformer = CreateBackendTransformer(properties);
        var finalModule = transformer.Apply(properties, typeInformationManager, module);

        if (dumpWriter is not null)
        {
            finalModule.Dump(
                dumpWriter,
                IRDumpMode.Normalized,
                format: dumpFormat,
                point: IRDumpPoint.AfterBackendTransforms,
                annotation: $"backend: {BackendType}  " +
                $"opt: {properties.OptimizationLevel}");
        }

        return GenerateCode(finalModule);
    }

    /// <summary>
    /// Generates code for the given module.
    /// </summary>
    /// <param name="module">The module to generate code for.</param>
    /// <returns>The generated code.</returns>
    protected virtual CodeGenerationResult GenerateCode(Module module)
    {
        var codeGenerator = new CodeGenerator(module, LanguageConfiguration);
        return codeGenerator.GenerateCode();
    }

    /// <summary>
    /// Compiles generated source code into a platform binary using the given compiler
    /// manager.
    /// </summary>
    /// <typeparam name="TManager">
    /// A type that implements <see cref="ICompilerManager"/>.
    /// </typeparam>
    /// <param name="source">The code generation result containing source code.</param>
    /// <param name="manager">The compiler manager to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The compilation result, or null if this backend does not support compilation.
    /// </returns>
    public virtual Task<CompilationResult?> CompileSourceAsync<TManager>(
        CodeGenerationResult source,
        TManager manager,
        CancellationToken ct = default)
        where TManager : ICompilerManager =>
        Task.FromResult<CompilationResult?>(null);
}

#pragma warning restore CA1515
