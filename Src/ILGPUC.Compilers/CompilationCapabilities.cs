// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationCapabilities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace ILGPUC.Compilers;

/// <summary>
/// Describes the capabilities of a single compiler.
/// </summary>
public sealed class CompilerCapability
{
    /// <summary>
    /// Gets the compilation target platform this capability describes.
    /// </summary>
    [JsonPropertyName("target")]
    public required CompilationTarget Target { get; init; }

    /// <summary>
    /// Gets a value indicating whether the compiler is available on this system.
    /// </summary>
    [JsonPropertyName("available")]
    public required bool Available { get; init; }

    /// <summary>
    /// Gets the compiler version string, or <c>null</c> if unavailable.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Gets the list of supported GPU architectures, or <c>null</c> if not reported.
    /// </summary>
    [JsonPropertyName("supportedArchitectures")]
    public string[]? SupportedArchitectures { get; init; }

    /// <summary>
    /// Gets the per-component toolchain health status, or <c>null</c> if not reported.
    /// </summary>
    [JsonPropertyName("toolchainComponents")]
    public ToolchainComponent[]? ToolchainComponents { get; init; }
}

/// <summary>
/// Describes the compilation capabilities of the service.
/// </summary>
public sealed class CompilationCapabilities
{
    /// <summary>
    /// Gets the set of per-compiler capability descriptors for all registered compilers.
    /// </summary>
    [JsonPropertyName("compilers")]
    public required CompilerCapability[] Compilers { get; init; }
}
