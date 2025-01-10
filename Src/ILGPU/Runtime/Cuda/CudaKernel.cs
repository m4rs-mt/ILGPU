// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda;

/// <summary>
/// Creates a new CudaCompiledKernel instance.
/// </summary>
/// <param name="data">Source data to create the kernel from.</param>
public sealed class CudaCompiledKernel(CompiledKernelData data) : CompiledKernel(data)
{
    /// <summary>
    /// Serializes custom attributes in terms of architecture and isa
    /// </summary>
    /// <param name="architecture">The architecture to serialize.</param>
    /// <param name="isa">The isa to serialize.</param>
    /// <returns>Serialized intermediate information.</returns>
    public static ReadOnlyMemory<byte> SerializeCustomAttributes(
        CudaArchitecture architecture,
        CudaInstructionSet isa)
    {
        var result = new byte[sizeof(int) * 4];
        var intSpan = result.AsSpan().CastUnsafe<byte, int>();

        // Store architecture
        intSpan[0] = architecture.Major;
        intSpan[1] = architecture.Minor;

        // Store isa
        intSpan[2] = isa.Major;
        intSpan[3] = isa.Minor;

        return result;
    }

    /// <summary>
    /// Deserializes architecture and ISA from the underlying kernel data.
    /// </summary>
    /// <returns>Deserialized architecture and ISA information.</returns>
    public (CudaArchitecture Architecture, CudaInstructionSet ISA)
        DeserializeCustomAttributes() =>
        DeserializeCustomAttributes(Data.CustomAttributes.Span);

    /// <summary>
    /// Deserializes architecture and ISA from the given custom attributes in serialized
    /// kernel form.
    /// </summary>
    /// <returns>Deserialized architecture and ISA information.</returns>
    public static (CudaArchitecture Architecture, CudaInstructionSet ISA)
        DeserializeCustomAttributes(ReadOnlySpan<byte> customAttributes)
    {
        var intSpan = customAttributes.CastUnsafe<byte, int>();

        // Load architecture and isa
        var arch = new CudaArchitecture(intSpan[0], intSpan[1]);
        var isa = new CudaInstructionSet(intSpan[2], intSpan[3]);

        return (arch, isa);
    }
}

/// <summary>
/// Represents a Cuda kernel that can be directly launched on a GPU.
/// </summary>
public sealed class CudaKernel : Kernel
{
    #region Instance

    /// <summary>
    /// Holds the pointer to the native Cuda module in memory.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IntPtr _modulePtr;

    /// <summary>
    /// Holds the pointer to the native Cuda function in memory.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IntPtr _functionPtr;

    /// <summary>
    /// Loads a compiled kernel into the given Cuda context as kernel program.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="kernel">The source kernel.</param>
    public CudaKernel(CudaAccelerator accelerator, CudaCompiledKernel kernel)
        : base(accelerator, kernel)
    {
        var kernelLoaded = CurrentAPI.LoadModule(
            out _modulePtr,
            kernel.GetSourceAsString(),
            out string? errorLog);
        if (kernelLoaded != CudaError.CUDA_SUCCESS)
        {
            Trace.WriteLine("PTX Kernel loading failed:");
            if (string.IsNullOrWhiteSpace(errorLog))
                Trace.WriteLine(">> No error information available");
            else
                Trace.WriteLine(errorLog);
        }
        CudaException.ThrowIfFailed(kernelLoaded);

        CudaException.ThrowIfFailed(
            CurrentAPI.GetModuleFunction(
                out _functionPtr,
                _modulePtr,
                kernel.KernelName));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the Cuda module pointer.
    /// </summary>
    public IntPtr ModulePtr => _modulePtr;

    /// <summary>
    /// Returns the Cuda function pointer.
    /// </summary>
    public IntPtr FunctionPtr => _functionPtr;

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes this Cuda kernel.
    /// </summary>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        CudaException.VerifyDisposed(
            disposing,
            CurrentAPI.DestroyModule(_modulePtr));
        _functionPtr = IntPtr.Zero;
        _modulePtr = IntPtr.Zero;
    }

    #endregion
}
