// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalCompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Specifies the target OS for Metal compilation.
/// </summary>
public enum MetalTargetOS
{
    /// <summary>macOS target.</summary>
    macOS,

    /// <summary>iOS target.</summary>
    iOS,
}

/// <summary>
/// Specifies the Metal GPU family for capability targeting.
/// </summary>
public enum MetalGPUFamily
{
    /// <summary>Apple GPU family 7 (A14, M1).</summary>
    Apple7,

    /// <summary>Apple GPU family 8 (A15, M2).</summary>
    Apple8,

    /// <summary>Apple GPU family 9 (A17 Pro, M3).</summary>
    Apple9,
}

/// <summary>
/// Represents a compiled Metal kernel with target OS and GPU family metadata.
/// </summary>
public class MetalCompiledKernel(
    Guid guid,
    string kernelName,
    CompiledKernelType kernelType,
    CompiledKernelSharedMemoryMode sharedMemoryMode,
    MetalTargetOS targetOS,
    MetalGPUFamily gpuFamily,
    string? source = null) :
    CompiledKernel(guid, kernelName, kernelType, sharedMemoryMode),
    ICompiledKernelKind
{
    /// <summary>
    /// Returns the <see cref="AcceleratorType.Metal"/> accelerator type.
    /// </summary>
    public static AcceleratorType GeneralAcceleratorType => AcceleratorType.Metal;

    /// <summary>
    /// The Metal shader source code.
    /// </summary>
    private readonly string? _source = source;

    /// <summary>
    /// Returns the target OS.
    /// </summary>
    public MetalTargetOS TargetOS { get; } = targetOS;

    /// <summary>
    /// Returns the target GPU family.
    /// </summary>
    public MetalGPUFamily GPUFamily { get; } = gpuFamily;

    /// <inheritdoc/>
    public override AcceleratorCapabilities RequiredCapabilities =>
        MetalAcceleratorCapabilities.FromGPUFamily(GPUFamily);

    /// <inheritdoc/>
    public override string GetSourceAsString() =>
        _source ?? throw new InvalidOperationException(
            "No source available. Use GetCompiledBinary() for pre-compiled kernels.");
}
