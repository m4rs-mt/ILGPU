// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecordingCompilerManager.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Compilers;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Records the compile request of a backend and reports a failed compilation,
/// so that tests can check the native compiler options without a toolchain.
/// </summary>
internal sealed class RecordingCompilerManager : ICompilerManager
{
    /// <summary>
    /// The last compile request.
    /// </summary>
    public CompileRequest? Request { get; private set; }

    /// <inheritdoc/>
    public Task<CompilationResult> CompileAsync(
        CompileRequest request,
        CancellationToken ct)
    {
        Request = request;
        return Task.FromResult(new CompilationResult { Success = false });
    }

    /// <inheritdoc/>
    public Task<CompilationCapabilities> GetCapabilitiesAsync(
        CancellationToken ct) =>
        Task.FromResult(new CompilationCapabilities { Compilers = [] });
}
