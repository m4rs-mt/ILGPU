// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Kernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime;

/// <summary>
/// Represents the base class for all runtime kernels.
/// </summary>
/// <param name="accelerator">The associated accelerator.</param>
/// <param name="compiledKernel">The source kernel.</param>
public abstract class Kernel(Accelerator accelerator, CompiledKernel compiledKernel) :
    AcceleratorObject(accelerator)
{
    /// <summary>
    /// Returns the underlying compiled kernel.
    /// </summary>
    public CompiledKernel CompiledKernel { get; } = compiledKernel;

    /// <summary>
    /// Returns assigned shared memory mode for kernel dispatch.
    /// </summary>
    public CompiledKernelSharedMemoryMode SharedMemoryMode { get; } =
        compiledKernel.Data.SharedMemoryMode;

    /// <summary>
    /// Returns assigned static shared memory in bytes.
    /// </summary>
    public int SharedMemorySize { get; } = compiledKernel.Data.SharedMemorySize;

    /// <summary>
    /// Returns assigned static local memory in bytes.
    /// </summary>
    public int LocalMemorySize { get; } = compiledKernel.Data.LocalMemorySize;

    /// <summary>
    /// Returns the desired maximum number of threads per group (if any).
    /// </summary>
    public int? MaxNumThreadsPerGroup { get; } = compiledKernel.MaxNumThreadsPerGroup;

    /// <summary>
    /// Combines static and dynamic shared memory information into the given kernel
    /// configuration to be used for kernel launch.
    /// </summary>
    /// <param name="config">The kernel configuration to adapt.</param>
    /// <returns>Combined kernel configuration information for launch.</returns>
    /// <exception cref="NotSupportedException">
    /// Will be thrown in case on an unsupported shared memory mode of this kernel.
    /// </exception>
    public KernelConfig GetCombinedSharedMemoryConfig(in KernelConfig config) =>
        SharedMemoryMode switch
        {
            CompiledKernelSharedMemoryMode.Static =>
                config with { SharedMemoryBytes = 0 },
            CompiledKernelSharedMemoryMode.Dynamic => config,
            CompiledKernelSharedMemoryMode.Hybrid =>
                config.WithSharedMemory(SharedMemorySize),
            _ => throw new NotSupportedException()
        };
}
