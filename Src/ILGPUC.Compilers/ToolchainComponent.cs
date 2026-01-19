// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ToolchainComponent.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace ILGPUC.Compilers;

/// <summary>
/// Describes the availability status of a single toolchain component
/// (e.g. <c>ptxas</c>, <c>metal</c>, <c>metallib</c>).
/// </summary>
public sealed class ToolchainComponent
{
    /// <summary>
    /// Gets the short name of the tool (e.g. <c>"ptxas"</c>, <c>"metal"</c>).
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tool was found and responded successfully.
    /// </summary>
    [JsonPropertyName("available")]
    public required bool Available { get; init; }

    /// <summary>
    /// Gets the first line of the tool's version output, or <c>null</c> if unavailable.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Gets a diagnostic message if the tool is unavailable, or <c>null</c> otherwise.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
