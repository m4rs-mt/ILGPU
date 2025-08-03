// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUCompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// Represents a compiled CSharp/Debug kernel for vectorized CPU execution.
/// </summary>
public class CPUCompiledKernel(
    Guid guid,
    string kernelName,
    CompiledKernelType kernelType,
    CompiledKernelSharedMemoryMode sharedMemoryMode,
    int simdWidth,
    string kernelClassName) :
    CompiledKernel(guid, kernelName, kernelType, sharedMemoryMode)
{
    /// <summary>
    /// Returns the SIMD width used for vectorized execution.
    /// </summary>
    public int SimdWidth { get; } = simdWidth;

    /// <summary>
    /// Returns the kernel class name for calling the entry point.
    /// </summary>
    public string KernelClassName { get; } = kernelClassName;

    /// <inheritdoc/>
    public override AcceleratorCapabilities RequiredCapabilities =>
        CPUAcceleratorCapabilities.Default;

    /// <inheritdoc/>
    public sealed override string GetSourceAsString() =>
        throw new NotSupportedException();
}
