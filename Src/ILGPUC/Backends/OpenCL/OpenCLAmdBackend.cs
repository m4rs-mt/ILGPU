// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: OpenCLAmdBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Backends.OpenCL;

#pragma warning disable CS9107

/// <summary>
/// OpenCL backend variant that compiles generated OpenCL C source to SPIR-V via
/// <c>clang -target spirv64-unknown-unknown</c>.
/// </summary>
sealed class OpenCLAmdBackend(int clcMajor, int clcMinor) :
    OpenCLBackend(clcMajor, clcMinor, CLVendor.Amd, warpSize: 64)
{
    /// <inheritdoc/>
    public override CompiledKernelEmitter CreateCompiledKernelEmitter() =>
        new OpenCLCompiledKernelEmitter(clcMajor, clcMinor, KernelEmbedMode.Binary);

    /// <inheritdoc/>
    public override async Task<CompilationResult?> CompileSourceAsync<TManager>(
        CodeGenerationResult source,
        TManager manager,
        CancellationToken ct = default)
    {
        return await manager.CompileAsync(new CompileRequest
        {
            SourceCode = source.SourceCode,
            Target = CompilationTarget.OpenCLAmd,
            OutputType = OutputType.SpirV,
        }, ct).ConfigureAwait(false);
    }
}

#pragma warning restore CS9107
