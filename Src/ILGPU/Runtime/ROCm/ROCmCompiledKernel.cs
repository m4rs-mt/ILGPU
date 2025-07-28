// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmCompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Represents a compiled ROCm kernel with target architecture metadata.
/// </summary>
public class ROCmCompiledKernel(
    Guid guid,
    string kernelName,
    CompiledKernelType kernelType,
    CompiledKernelSharedMemoryMode sharedMemoryMode,
    string targetArchitecture) :
    CompiledKernel(guid, kernelName, kernelType, sharedMemoryMode),
    ICompiledKernelKind
{
    /// <summary>
    /// Returns the <see cref="AcceleratorType.ROCm"/> accelerator type.
    /// </summary>
    public static AcceleratorType GeneralAcceleratorType => AcceleratorType.ROCm;

    /// <summary>
    /// Returns the target architecture string (e.g., "gfx1100").
    /// </summary>
    public string TargetArchitecture { get; } = targetArchitecture;

    /// <inheritdoc/>
    public override AcceleratorCapabilities RequiredCapabilities =>
        ROCmAcceleratorCapabilities.FromArchitectureString(TargetArchitecture);

    /// <inheritdoc/>
    public override string GetSourceAsString() =>
        throw new InvalidOperationException(
            "Override GetSourceAsString in derived class.");
}
