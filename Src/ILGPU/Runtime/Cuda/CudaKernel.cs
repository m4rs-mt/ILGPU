// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2017-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda;

/// <summary>
/// Represents a compiled CUDA kernel with architecture and ISA metadata.
/// </summary>
public class CudaCompiledKernel(
    Guid guid,
    string kernelName,
    CompiledKernelType kernelType,
    CompiledKernelSharedMemoryMode sharedMemoryMode,
    AcceleratorArchitecture architecture,
    CudaInstructionSet instructionSet) :
    CompiledKernel(guid, kernelName, kernelType, sharedMemoryMode),
    ICompiledKernelKind
{
    /// <summary>
    /// Returns the <see cref="AcceleratorType.Cuda"/> accelerator type.
    /// </summary>
    public static AcceleratorType GeneralAcceleratorType => AcceleratorType.Cuda;

    /// <summary>
    /// Returns the target architecture.
    /// </summary>
    public AcceleratorArchitecture Architecture { get; } = architecture;

    /// <summary>
    /// Returns the target instruction set.
    /// </summary>
    public CudaInstructionSet InstructionSet { get; } = instructionSet;

    /// <inheritdoc/>
    public override AcceleratorCapabilities RequiredCapabilities =>
        CudaAcceleratorCapabilities.FromArchitecture(Architecture);

    /// <inheritdoc/>
    public override string GetSourceAsString() =>
        throw new InvalidOperationException(
            "Override GetSourceAsString in derived class.");
}

/// <summary>
/// Represents a Cuda kernel that can be directly launched on a GPU.
/// </summary>
public sealed class CudaKernel : Kernel
{
    #region Instance

    /// <summary>
    /// Loads a compiled kernel into the given Cuda context as kernel program.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="kernel">The source kernel.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public CudaKernel(CudaAccelerator accelerator, CudaCompiledKernel kernel)
        : base(accelerator, kernel)
    {
        var binary = kernel.GetCompiledBinary();
        var kernelLoaded = CurrentAPI.LoadModule(
            out var modulePtr,
            binary.Span,
            out string? errorLog);
        if (kernelLoaded != CudaError.CUDA_SUCCESS)
        {
            Trace.WriteLine("Kernel loading failed:");
            if (string.IsNullOrWhiteSpace(errorLog))
                Trace.WriteLine(">> No error information available");
            else
                Trace.WriteLine(errorLog);
        }
        CudaException.ThrowIfFailed(kernelLoaded);

        CudaException.ThrowIfFailed(
            CurrentAPI.GetModuleFunction(
                out var functionPtr,
                modulePtr,
                kernel.KernelName));

        ModulePtr = modulePtr;
        FunctionPtr = functionPtr;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the Cuda module pointer.
    /// </summary>
    public IntPtr ModulePtr { get; private set; }

    /// <summary>
    /// Returns the Cuda function pointer.
    /// </summary>
    public IntPtr FunctionPtr { get; private set; }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes this Cuda kernel.
    /// </summary>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        CudaException.VerifyDisposed(
            disposing,
            CurrentAPI.DestroyModule(ModulePtr));
        FunctionPtr = IntPtr.Zero;
        ModulePtr = IntPtr.Zero;
    }

    #endregion
}
