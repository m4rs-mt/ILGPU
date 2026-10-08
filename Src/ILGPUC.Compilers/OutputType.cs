// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: OutputType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace ILGPUC.Compilers;

/// <summary>
/// The desired output format from compilation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutputType
{
    /// <summary>
    /// A platform-native compiled binary (cubin, object file, or Metal library).
    /// </summary>
    Binary,

    /// <summary>
    /// NVIDIA PTX (Parallel Thread Execution) intermediate assembly text.
    /// </summary>
    Ptx,

    /// <summary>SPIR-V intermediate representation bytecode.</summary>
    SpirV,
}
