// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL;

/// <summary>
/// Represents a compiled OpenCL kernel with CLC version metadata.
/// </summary>
public class CLCompiledKernel(
    Guid guid,
    string kernelName,
    CompiledKernelType kernelType,
    CompiledKernelSharedMemoryMode sharedMemoryMode,
    CLCVersion cVersion,
    string? source = null) :
    CompiledKernel(guid, kernelName, kernelType, sharedMemoryMode),
    ICompiledKernelKind
{
    /// <summary>
    /// Returns the <see cref="AcceleratorType.OpenCL"/> accelerator type.
    /// </summary>
    public static AcceleratorType GeneralAcceleratorType => AcceleratorType.OpenCL;

    private readonly string? _source = source;

    /// <summary>
    /// Returns the necessary CLC version.
    /// </summary>
    public CLCVersion CVersion { get; } = cVersion;

    /// <inheritdoc/>
    public override AcceleratorCapabilities RequiredCapabilities =>
        new CLAcceleratorCapabilities();

    /// <inheritdoc/>
    public override string GetSourceAsString() =>
        _source ?? throw new InvalidOperationException(
            "No source available. Use GetCompiledBinary() for pre-compiled kernels.");
}

/// <summary>
/// Represents an OpenCL kernel that can be directly launched on an OpenCL device.
/// </summary>
public sealed class CLKernel : Kernel
{
    #region Static

    /// <summary>
    /// Loads the given OpenCL kernel.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="name">The name of the entry-point function.</param>
    /// <param name="source">The OpenCL source code.</param>
    /// <param name="version">The OpenCL C version.</param>
    /// <param name="programPtr">The created program pointer.</param>
    /// <param name="kernelPtr">The created kernel pointer.</param>
    /// <param name="errorLog">The error log (if any).</param>
    /// <returns>
    /// True, if the program and the kernel could be loaded successfully.
    /// </returns>
    internal static CLError LoadKernel(
        CLAccelerator accelerator,
        string name,
        string source,
        CLCVersion version,
        out IntPtr programPtr,
        out IntPtr kernelPtr,
        out string? errorLog)
    {
        errorLog = null;
        kernelPtr = IntPtr.Zero;
        var programError = CurrentAPI.CreateProgram(
            accelerator.NativePtr,
            source,
            out programPtr);
        if (programError != CLError.CL_SUCCESS)
            return programError;

        // Specify the OpenCL C version.
        string options = "-cl-std=" + version.ToString();

        var buildError = CurrentAPI.BuildProgram(
            programPtr,
            accelerator.DeviceId,
            options);

        if (buildError != CLError.CL_SUCCESS)
        {
            CLException.ThrowIfFailed(
                CurrentAPI.GetProgramBuildLog(
                    programPtr,
                    accelerator.DeviceId,
                    out errorLog));
            CLException.ThrowIfFailed(
                CurrentAPI.ReleaseProgram(programPtr));
            programPtr = IntPtr.Zero;
            return buildError;
        }

        return CurrentAPI.CreateKernel(
            programPtr,
            name,
            out kernelPtr);
    }

    /// <summary>
    /// Loads an OpenCL kernel from SPIR-V intermediate language.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="name">The name of the entry-point function.</param>
    /// <param name="il">The SPIR-V binary.</param>
    /// <param name="programPtr">The created program pointer.</param>
    /// <param name="kernelPtr">The created kernel pointer.</param>
    /// <param name="errorLog">The error log (if any).</param>
    /// <returns>The error code.</returns>
    internal static CLError LoadKernelFromIL(
        CLAccelerator accelerator,
        string name,
        ReadOnlySpan<byte> il,
        out IntPtr programPtr,
        out IntPtr kernelPtr,
        out string? errorLog)
    {
        errorLog = null;
        kernelPtr = IntPtr.Zero;
        var programError = CurrentAPI.CreateProgramWithIL(
            accelerator.NativePtr,
            il,
            out programPtr);
        if (programError != CLError.CL_SUCCESS)
            return programError;

        // SPIR-V programs still need clBuildProgram but no -cl-std flag
        var buildError = CurrentAPI.BuildProgram(
            programPtr,
            accelerator.DeviceId,
            string.Empty);

        if (buildError != CLError.CL_SUCCESS)
        {
            CLException.ThrowIfFailed(
                CurrentAPI.GetProgramBuildLog(
                    programPtr,
                    accelerator.DeviceId,
                    out errorLog));
            CLException.ThrowIfFailed(
                CurrentAPI.ReleaseProgram(programPtr));
            programPtr = IntPtr.Zero;
            return buildError;
        }

        return CurrentAPI.CreateKernel(programPtr, name, out kernelPtr);
    }

    /// <summary>
    /// Loads the binary representation of the given OpenCL kernel.
    /// </summary>
    /// <param name="program">The program pointer.</param>
    /// <returns>The binary representation of the underlying kernel.</returns>
    public static unsafe ReadOnlyMemory<byte> LoadBinaryRepresentation(IntPtr program)
    {
        IntPtr kernelSize;
        CLException.ThrowIfFailed(
            CurrentAPI.GetProgramInfo(
                program,
                CLProgramInfo.CL_PROGRAM_BINARY_SIZES,
                new IntPtr(IntPtr.Size),
                &kernelSize,
                out var _));

        var programBinary = new byte[kernelSize.ToInt32()];
        fixed (byte* binPtr = &programBinary[0])
        {
            CLException.ThrowIfFailed(
                CurrentAPI.GetProgramInfo(
                    program,
                    CLProgramInfo.CL_PROGRAM_BINARIES,
                    new IntPtr(IntPtr.Size),
                    &binPtr,
                    out var _));
        }
        return programBinary;
    }

    #endregion

    #region Instance

    /// <summary>
    /// Holds the pointer to the native OpenCL program in memory.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IntPtr _programPtr;

    /// <summary>
    /// Holds the pointer to the native OpenCL kernel in memory.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IntPtr _kernelPtr;

    /// <summary>
    /// Loads a compiled kernel into the given OpenCL context as kernel program.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="kernel">The source kernel.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public CLKernel(CLAccelerator accelerator, CLCompiledKernel kernel)
        : base(accelerator, kernel)
    {
        // Try SPIR-V binary path first
        ReadOnlyMemory<byte> binary = default;
        try { binary = kernel.GetCompiledBinary(); }
        catch (NotSupportedException) { }

        var errorCode = binary.Length > 0
            ? LoadKernelFromIL(
                accelerator, kernel.KernelName, binary.Span,
                out _programPtr, out _kernelPtr, out string? errorLog)
            : LoadKernel(
                accelerator, kernel.KernelName,
                kernel.GetSourceAsString(), kernel.CVersion,
                out _programPtr, out _kernelPtr, out errorLog);

        if (errorCode != CLError.CL_SUCCESS)
        {
            Trace.WriteLine("Kernel loading failed:");
            if (string.IsNullOrWhiteSpace(errorLog))
                Trace.WriteLine(">> No error information available");
            else
                Trace.WriteLine(errorLog);
        }
        CLException.ThrowIfFailed(errorCode);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the OpenCL program ptr.
    /// </summary>
    public IntPtr ProgramPtr => _programPtr;

    /// <summary>
    /// Returns the OpenCL kernel ptr.
    /// </summary>
    public IntPtr KernelPtr => _kernelPtr;

    #endregion

    #region Methods

    /// <summary>
    /// Loads the binary representation of the underlying OpenCL kernel.
    /// </summary>
    /// <returns>The binary representation of the underlying kernel.</returns>
    public ReadOnlyMemory<byte> LoadBinaryRepresentation() =>
        LoadBinaryRepresentation(ProgramPtr);

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes this OpenCL kernel.
    /// </summary>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        // Free the kernel
        if (_kernelPtr != IntPtr.Zero)
        {
            CLException.VerifyDisposed(
                disposing,
                CurrentAPI.ReleaseKernel(_kernelPtr));
            _kernelPtr = IntPtr.Zero;
        }

        // Free the surrounding program
        if (_programPtr != IntPtr.Zero)
        {
            CLException.VerifyDisposed(
                disposing,
                CurrentAPI.ReleaseProgram(_programPtr));
            _programPtr = IntPtr.Zero;
        }
    }

    #endregion
}
