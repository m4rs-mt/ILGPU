// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationTarget.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace ILGPUC.Compilers;

/// <summary>
/// The GPU compilation target platform.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompilationTarget
{
    /// <summary>NVIDIA CUDA, compiled via <c>nvcc</c>.</summary>
    Cuda,

    /// <summary>AMD HIP, compiled via <c>hipcc</c>.</summary>
    Hip,

    /// <summary>Apple Metal, compiled via <c>xcrun metal</c>.</summary>
    Metal,

    /// <summary>Intel GPU via <c>ocloc</c>, producing SPIR-V.</summary>
    OpenCLIntel,

    /// <summary>AMD GPU via <c>clang</c>, producing SPIR-V.</summary>
    OpenCLAmd,
}
