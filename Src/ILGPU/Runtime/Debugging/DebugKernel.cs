// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Debugging;

/// <summary>
/// Wraps kernel groups to be invoked.
/// </summary>
/// <param name="kernelConfig">The current kernel config.</param>
/// <param name="gridIndex">The current grid index of the current group.</param>
/// <param name="context">The current kernel context.</param>
public delegate void DebugKernelGroupHandler(
    in KernelConfig kernelConfig,
    long gridIndex,
    DebugKernelContext context);

/// <summary>
/// Represents a single debug kernel.
/// </summary>
public sealed class DebugCompiledKernel(
    CompiledKernelData data,
    DebugKernelGroupHandler groupHandler) : CompiledKernel(data)
{
    /// <summary>
    /// Returns the underlying group handler.
    /// </summary>
    public DebugKernelGroupHandler GroupHandler { get; } = groupHandler;
}

/// <summary>
/// An abstract debug kernel context to be used during kernel launch.
/// </summary>
public abstract class DebugKernelContext;

/// <summary>
/// Represents a single debug kernel.
/// </summary>
public sealed class DebugKernel : Kernel
{
    private readonly DebugKernelGroupHandler _handler;

    /// <summary>
    /// Loads a compiled kernel into the given Cuda context as kernel program.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="kernel">The source kernel.</param>
    internal DebugKernel(DebugAccelerator accelerator, CompiledKernel kernel)
        : base(accelerator, kernel)
    {
        _handler = ((DebugCompiledKernel)kernel).GroupHandler;
    }

    /// <summary>
    /// Invokes the group processing for the current thread.
    /// </summary>
    /// <param name="kernelConfig">The current kernel config.</param>
    /// <param name="gridIndex">The grid index to use.</param>
    /// <param name="context">The current kernel context.</param>
    public void InvokeGroup(
        in KernelConfig kernelConfig,
        long gridIndex,
        DebugKernelContext context) =>
        _handler(kernelConfig, gridIndex, context);

    /// <summary>
    /// Does not perform any operation.
    /// </summary>
    protected override void DisposeAcceleratorObject(bool disposing) { }
}
