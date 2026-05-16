// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
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
/// The optimization level passed to the underlying native compiler. Values map
/// directly to the <c>-O0</c>..<c>-O3</c> flags used by the gcc/clang/nvcc/hipcc
/// family. ILGPU backends translate the IR-level optimization level into a value
/// of this enum before submitting a request.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NativeOptimizationLevel
{
    /// <summary>No native optimization (<c>-O0</c>).</summary>
    O0 = 0,

    /// <summary>Basic native optimization (<c>-O1</c>).</summary>
    O1 = 1,

    /// <summary>Default release-quality native optimization (<c>-O2</c>).</summary>
    O2 = 2,

    /// <summary>Maximum native optimization (<c>-O3</c>).</summary>
    O3 = 3,
}

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
    /// Gets the optimization level to pass to the native compiler. Defaults to
    /// <see cref="NativeOptimizationLevel.O2"/>.
    /// </summary>
    [JsonPropertyName("optimizationLevel")]
    public NativeOptimizationLevel OptimizationLevel { get; init; } =
        NativeOptimizationLevel.O2;

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
    /// Gets optional extra backend-specific flags forwarded verbatim to the underlying
    /// compiler after sanitization. Used for things like nvcc's
    /// <c>-gencode=arch=...,code=...</c>. <see cref="OptimizationLevel"/> is handled
    /// separately and should not be specified here.
    /// </summary>
    [JsonPropertyName("extraFlags")]
    public string? ExtraFlags { get; init; }

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
