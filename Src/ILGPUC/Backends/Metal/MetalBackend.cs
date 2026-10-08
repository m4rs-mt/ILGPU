// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Metal;
using ILGPUC.Compilers;
using ILGPUC.IR.Transformations;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Backends.Metal;

/// <summary>
/// Represents an ILGPU backend for Metal.
/// </summary>
/// <param name="targetOS">The target platform (macOS or iOS).</param>
/// <param name="gpuFamily">The target GPU family.</param>
sealed class MetalBackend(MetalTargetOS targetOS, MetalGPUFamily gpuFamily) :
    Backend(
        BackendType.Metal,
        AcceleratorType.Metal,
        MetalAcceleratorCapabilities.FromGPUFamily(gpuFamily))
{
    /// <summary>
    /// Returns the target platform.
    /// </summary>
    public MetalTargetOS TargetOS => targetOS;

    /// <summary>
    /// Returns the target GPU family.
    /// </summary>
    public MetalGPUFamily GPUFamily => gpuFamily;

    /// <summary>
    /// Returns the underlying language configuration.
    /// </summary>
    public override LanguageConfiguration LanguageConfiguration { get; } =
        new MetalLanguageConfiguration();

    /// <inheritdoc/>
    public override LauncherEmitter CreateLauncherEmitter() =>
        new MetalLauncherEmitter();

    /// <summary>
    /// Returns the warp size (SIMD width) for Metal. Apple Silicon GPUs
    /// always use a thread execution width of 32.
    /// </summary>
    public override int? CurrentWarpSize => 32;

    /// <inheritdoc/>
    public override CompiledKernelEmitter CreateCompiledKernelEmitter() =>
        new MetalCompiledKernelEmitter(targetOS, gpuFamily);

    /// <inheritdoc/>
    protected override ArchitectureSpecification GetArchitectureSpecification() =>
        new(Capabilities, CurrentWarpSize, new(0, 0), SupportsViews: false);

    /// <inheritdoc/>
    public override async Task<CompilationResult?> CompileSourceAsync<TManager>(
        CodeGenerationResult source,
        TManager manager,
        CancellationToken ct = default)
    {
        return await manager.CompileAsync(new CompileRequest
        {
            SourceCode = source.SourceCode,
            Target = CompilationTarget.Metal,
            OutputType = OutputType.Binary,
        }, ct).ConfigureAwait(false);
    }
}
