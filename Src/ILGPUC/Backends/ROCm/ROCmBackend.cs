// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.ROCm;
using ILGPU.Util;
using ILGPUC.Compilers;
using ILGPUC.IR.Transformations;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Backends.ROCm;

/// <summary>
/// Represents an ILGPU backend for HIP/ROCm.
/// </summary>
/// <remarks>
/// Constructs a new generic backend.
/// </remarks>
/// <param name="arch">The ROCm architecture.</param>
sealed class ROCmBackend(string arch) :
    Backend(
        BackendType.ROCm,
        AcceleratorType.ROCm,
        ROCmAcceleratorCapabilities.FromArchitectureString(arch))
{
    /// <summary>
    /// Returns the current warp size (if available).
    /// </summary>
    public override int? CurrentWarpSize =>
        Capabilities.AsNotNullCast<ROCmAcceleratorCapabilities>().WavefrontSize;

    /// <summary>
    /// Returns the underlying language configuration.
    /// </summary>
    public override LanguageConfiguration LanguageConfiguration { get; } =
        new ROCmLanguageConfiguration();

    /// <inheritdoc/>
    public override LauncherEmitter CreateLauncherEmitter() =>
        new ROCmLauncherEmitter();

    /// <inheritdoc/>
    public override CompiledKernelEmitter CreateCompiledKernelEmitter() =>
        new ROCmCompiledKernelEmitter(arch);

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
            Target = CompilationTarget.Hip,
            OutputType = OutputType.Binary,
        }, ct).ConfigureAwait(false);
    }
}
