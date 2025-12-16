// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationResult.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace ILGPUC.Compilers;

/// <summary>
/// The result of a GPU compilation.
/// </summary>
public sealed class CompilationResult
{
    /// <summary>
    /// Gets a value indicating whether the compilation succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the base-64-encoded compiled binary output, or <c>null</c> on failure.
    /// </summary>
    [JsonPropertyName("output")]
    public string? Output { get; init; }

    /// <summary>
    /// Gets the standard output captured from the compiler process.
    /// </summary>
    [JsonPropertyName("stdOut")]
    public string? StdOut { get; init; }

    /// <summary>
    /// Gets the standard error captured from the compiler process.
    /// </summary>
    [JsonPropertyName("stdErr")]
    public string? StdErr { get; init; }

    /// <summary>
    /// Gets the process exit code returned by the compiler.
    /// </summary>
    [JsonPropertyName("exitCode")]
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets the compilation target platform for which the source was compiled.
    /// </summary>
    [JsonPropertyName("target")]
    public CompilationTarget Target { get; init; }

    /// <summary>
    /// Gets the output format that was produced by the compiler.
    /// </summary>
    [JsonPropertyName("outputType")]
    public OutputType OutputType { get; init; }

    /// <summary>
    /// Gets a value indicating whether this result was served from a cache.
    /// </summary>
    [JsonPropertyName("isCached")]
    public bool IsCached { get; init; }
}
