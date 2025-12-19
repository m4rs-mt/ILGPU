// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompileRequest.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace ILGPUC.Compilers;

/// <summary>
/// A request to compile GPU source code.
/// </summary>
public sealed class CompileRequest
{
    /// <summary>
    /// Gets the GPU source code to compile.
    /// </summary>
    [JsonPropertyName("sourceCode")]
    public required string SourceCode { get; init; }

    /// <summary>
    /// Gets the target GPU platform (CUDA, HIP, or Metal).
    /// </summary>
    [JsonPropertyName("target")]
    public required CompilationTarget Target { get; init; }

    /// <summary>
    /// Gets the desired output format. Defaults to <see cref="OutputType.Binary"/>.
    /// </summary>
    [JsonPropertyName("outputType")]
    public OutputType OutputType { get; init; } = OutputType.Binary;

    /// <summary>
    /// Gets an optional set of additional include-search paths to pass to the compiler.
    /// </summary>
    [JsonPropertyName("includePaths")]
    public string[]? IncludePaths { get; init; }

    /// <summary>
    /// Gets an optional set of preprocessor directives to define for the compilation.
    /// </summary>
    [JsonPropertyName("compilerDirectives")]
    public string[]? CompilerDirectives { get; init; }

    /// <summary>
    /// Gets optional extra flags forwarded verbatim to the underlying compiler after
    /// sanitization.
    /// </summary>
    [JsonPropertyName("cudaFlags")]
    public string? CudaFlags { get; init; }

    /// <summary>
    /// Gets a value indicating whether the compilation should be dispatched
    /// asynchronously and polled later.
    /// </summary>
    [JsonPropertyName("asyncMode")]
    public bool AsyncMode { get; init; }

    /// <summary>
    /// Gets an optional caller-assigned identifier for tracking asynchronous jobs.
    /// </summary>
    [JsonPropertyName("jobId")]
    public string? JobId { get; init; }
}
