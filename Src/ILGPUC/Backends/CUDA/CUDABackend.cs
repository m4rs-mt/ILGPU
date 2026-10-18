// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CUDABackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPUC.Compilers;
using ILGPUC.IR.Transformations;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Backends.Cuda;

/// <summary>
/// Represents a general ILGPU backend.
/// </summary>
/// <remarks>
/// Constructs a new generic backend.
/// </remarks>
/// <param name="arch">The Cuda architecture.</param>
/// <param name="isa">The Cuda ISA.</param>
sealed class CudaBackend(AcceleratorArchitecture arch, CudaInstructionSet isa) :
    Backend(
        BackendType.Cuda,
        AcceleratorType.Cuda,
        CudaAcceleratorCapabilities.FromArchitecture(arch))
{
    /// <summary>
    /// Returns the current warp size (if available).
    /// </summary>
    public override int? CurrentWarpSize => 32;

    /// <summary>
    /// Returns the underlying language configuration.
    /// </summary>
    public override LanguageConfiguration LanguageConfiguration { get; } =
        new CudaLanguageConfiguration();

    /// <inheritdoc/>
    public override LauncherEmitter CreateLauncherEmitter() =>
        new CudaLauncherEmitter();

    /// <inheritdoc/>
    public override CompiledKernelEmitter CreateCompiledKernelEmitter() =>
        new CudaCompiledKernelEmitter(arch, isa);

    /// <inheritdoc/>
    protected override ArchitectureSpecification GetArchitectureSpecification() =>
        new(Capabilities, CurrentWarpSize, arch, SupportsViews: false);

    /// <inheritdoc/>
    public override async Task<CompilationResult?> CompileSourceAsync<TManager>(
        CodeGenerationResult source,
        TManager manager,
        CompilationProperties properties,
        CancellationToken ct = default)
    {
        // Build a PTX-only fatbin keyed to the configured virtual
        // architecture. The CUDA driver JITs PTX at module-load time, so
        // the resulting binary loads on any device with a compute
        // capability >= the requested virtual architecture (matching what
        // the runtime kernel-loading path expects). Without an explicit
        // -gencode flag nvcc defaults to a SASS-only cubin for a fixed
        // architecture that refuses to load on newer hardware.
        var computeTag = $"compute_{arch.Major}{arch.Minor}";
        var archFlags = $"-gencode=arch={computeTag},code={computeTag}";
        return await manager.CompileAsync(new CompileRequest
        {
            SourceCode = source.SourceCode,
            Target = CompilationTarget.Cuda,
            OutputType = OutputType.Binary,
            OptimizationLevel = GetNativeOptLevel(properties.OptimizationLevel),
            ExtraFlags = archFlags,
        }, ct).ConfigureAwait(false);
    }
}
