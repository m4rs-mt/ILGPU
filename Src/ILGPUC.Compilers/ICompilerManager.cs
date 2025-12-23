// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ICompilerManager.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Manages a collection of GPU compilers and routes compilation requests to the
/// appropriate backend.
/// </summary>
public interface ICompilerManager
{
    /// <summary>
    /// Compiles the GPU source code described by <paramref name="request"/> using the
    /// compiler registered for the requested target.
    /// </summary>
    /// <param name="request">
    /// The compilation request containing source and options.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A <see cref="CompilationResult"/> describing the outcome.</returns>
    Task<CompilationResult> CompileAsync(CompileRequest request, CancellationToken ct);

    /// <summary>
    /// Queries all registered compilers and returns an aggregate report of their
    /// availability and version information.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="CompilationCapabilities"/> containing one entry per registered
    /// compiler.
    /// </returns>
    Task<CompilationCapabilities> GetCapabilitiesAsync(CancellationToken ct);
}
