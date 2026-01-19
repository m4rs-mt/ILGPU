// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ICompiler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Abstraction over a single GPU compiler tool (e.g. nvcc, hipcc, xcrun/metal).
/// </summary>
public interface ICompiler
{
    /// <summary>
    /// Gets the GPU platform this compiler targets.
    /// </summary>
    CompilationTarget Target { get; }

    /// <summary>
    /// Gets a value indicating whether the compiler executable exists and responds to
    /// a basic invocation on the current system.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Compiles the GPU source code described by <paramref name="request"/>.
    /// </summary>
    /// <param name="request">
    /// The compilation request containing source and options.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A <see cref="CompilationResult"/> describing the outcome.</returns>
    Task<CompilationResult> CompileAsync(CompileRequest request, CancellationToken ct);

    /// <summary>
    /// Returns a short human-readable version string for the compiler, or <c>null</c>
    /// if the compiler is unavailable or the query fails.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// The first line of the compiler's <c>--version</c> output, or <c>null</c>.
    /// </returns>
    Task<string?> GetVersionAsync(CancellationToken ct);

    /// <summary>
    /// Gets the per-component toolchain health status. Returns an empty array by
    /// default for compilers that do not report individual component status.
    /// </summary>
    ToolchainComponent[] ToolchainComponents => [];
}
