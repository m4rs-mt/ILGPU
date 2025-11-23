// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompiledKernelEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPUC.Backends;

/// <summary>
/// Specifies how kernel code is embedded in the generated compiled kernel class.
/// </summary>
enum KernelEmbedMode
{
    /// <summary>
    /// Embed kernel source as a verbatim string constant (OpenCL, fallback).
    /// </summary>
    Source,

    /// <summary>
    /// Embed pre-compiled binary as a base64-encoded byte array (Cuda, ROCm, Metal).
    /// </summary>
    Binary,

    /// <summary>
    /// Inline kernel code directly as a nested class (CPU backend).
    /// </summary>
    InlineCode,
}

/// <summary>
/// Abstract base for platform-specific compiled kernel class emission strategies.
/// Each backend provides an implementation that knows how to emit the
/// backend-specific base class, constructor arguments, and usings.
/// </summary>
abstract class CompiledKernelEmitter
{
    /// <summary>
    /// Returns the base class name for the generated compiled kernel class
    /// (e.g., "CudaCompiledKernel").
    /// </summary>
    public abstract string BaseClassName { get; }

    /// <summary>
    /// Returns the accelerator type for this backend.
    /// </summary>
    public abstract AcceleratorType AcceleratorType { get; }

    /// <summary>
    /// Returns additional using directives needed for the generated file.
    /// </summary>
    public abstract string[] RequiredUsings { get; }

    /// <summary>
    /// Emits platform-specific constructor arguments (after the standard ones)
    /// as source code lines.
    /// </summary>
    /// <param name="ctx">The emission context for writing code.</param>
    public abstract void EmitPlatformConstructorArgs(
        LauncherEmissionContext ctx);

    /// <summary>
    /// Returns the preferred kernel embedding mode for this backend.
    /// </summary>
    public virtual KernelEmbedMode EmbedMode => KernelEmbedMode.Source;

    /// <summary>
    /// Emits the <c>RequiredCapabilities</c> property override for the generated
    /// compiled kernel class.
    /// </summary>
    /// <param name="ctx">The emission context for writing code.</param>
    public abstract void EmitRequiredCapabilitiesProperty(LauncherEmissionContext ctx);
}
