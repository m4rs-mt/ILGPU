// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilerOptions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Compilers;

/// <summary>
/// Configurable paths for GPU compilers.
/// </summary>
public sealed class CompilerOptions
{
    /// <summary>
    /// Gets the absolute path to the <c>nvcc</c> CUDA compiler executable.
    /// Defaults to <c>/usr/local/cuda/bin/nvcc</c>.
    /// </summary>
    public string NvccPath { get; init; } = "/usr/local/cuda/bin/nvcc";

    /// <summary>
    /// Gets the absolute path to the <c>hipcc</c> HIP compiler executable.
    /// Defaults to <c>/opt/rocm/bin/hipcc</c>.
    /// </summary>
    public string HipccPath { get; init; } = "/opt/rocm/bin/hipcc";

    /// <summary>
    /// Gets the absolute path to the <c>xcrun</c> tool used to invoke the Metal compiler.
    /// Defaults to <c>/usr/bin/xcrun</c>.
    /// </summary>
    public string XcrunPath { get; init; } = "/usr/bin/xcrun";

    /// <summary>
    /// Gets the Apple SDK name passed to <c>xcrun -sdk</c> when compiling Metal shaders.
    /// Defaults to <c>macosx</c>.
    /// </summary>
    public string MetalSdk { get; init; } = "macosx";

    /// <summary>
    /// Gets the absolute path to the <c>ocloc</c> Intel OpenCL offline compiler executable.
    /// Defaults to <c>/usr/bin/ocloc</c>.
    /// </summary>
    public string OclocPath { get; init; } = "/usr/bin/ocloc";

    /// <summary>
    /// Gets the absolute path to the <c>clang</c> compiler used for OpenCL C to SPIR-V
    /// compilation. Defaults to <c>/usr/bin/clang</c>.
    /// </summary>
    public string ClangPath { get; init; } = "/usr/bin/clang";
}
