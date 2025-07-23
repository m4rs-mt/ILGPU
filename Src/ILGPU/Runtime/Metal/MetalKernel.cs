// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Represents a loaded Metal compute kernel backed by an MTLComputePipelineState.
/// </summary>
public sealed class MetalKernel : Kernel
{
    /// <summary>
    /// Creates a new Metal kernel by compiling the source and creating a
    /// compute pipeline state.
    /// </summary>
    /// <param name="accelerator">The parent Metal accelerator.</param>
    /// <param name="compiledKernel">The compiled kernel containing source code.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal MetalKernel(
        MetalAccelerator accelerator,
        MetalCompiledKernel compiledKernel)
        : base(accelerator, compiledKernel)
    {
        var deviceHandle = accelerator.DeviceHandle;

        // Load the Metal library — prefer pre-compiled binary, fall back
        // to runtime source compilation.
        LibraryPtr = LoadLibrary(compiledKernel, deviceHandle);

        // Get the kernel function by name.
        var nsName = MetalAPI.CreateNSString(compiledKernel.KernelName);
        try
        {
            var function = MetalAPI.NewFunctionWithName(LibraryPtr, nsName);
            MetalException.ThrowIfNull(
                function,
                IntPtr.Zero,
                MetalError.FunctionNotFound,
                $"Function '{compiledKernel.KernelName}' not found in " +
                "Metal library");
            FunctionPtr = function;
        }
        finally
        {
            MetalAPI.Release(nsName);
        }

        // Create the compute pipeline state.
        var pipelineState = MetalAPI.NewComputePipelineState(
            deviceHandle, FunctionPtr, out var pipelineError);
        if (pipelineState == IntPtr.Zero)
        {
            var errorMsg = MetalAPI.GetNSErrorDescription(pipelineError);
            Trace.WriteLine("Metal pipeline state creation failed:");
            Trace.WriteLine(errorMsg ?? ">> No error information available");
            if (pipelineError != IntPtr.Zero)
                MetalAPI.Release(pipelineError);
            throw new MetalException(
                MetalError.PipelineCreationFailed,
                $"Failed to create Metal pipeline state: {errorMsg}");
        }
        PipelineState = pipelineState;
    }

    /// <summary>
    /// Loads a Metal library from binary data or source code.
    /// </summary>
    private static IntPtr LoadLibrary(
        MetalCompiledKernel compiledKernel,
        IntPtr deviceHandle)
    {
        // Try pre-compiled binary first (metallib)
        try
        {
            var binary = compiledKernel.GetCompiledBinary();
            if (binary.Length > 0)
            {
                var library = MetalAPI.NewLibraryWithData(
                    deviceHandle, binary.Span, out var error);
                if (library != IntPtr.Zero)
                    return library;

                var errorMsg = MetalAPI.GetNSErrorDescription(error);
                if (error != IntPtr.Zero)
                    MetalAPI.Release(error);
                var msg = $"Metal binary library load failed " +
                    $"({binary.Length} bytes), lib=0x{library:X}, " +
                    $"err=0x{error:X}: {errorMsg}";
                Trace.WriteLine(msg);
                Console.Error.WriteLine(msg);
            }
        }
        catch (NotSupportedException)
        {
            // No binary available — fall through to source compilation
        }

        // Fall back to runtime source compilation
        var source = compiledKernel.GetSourceAsString();
        var nsSource = MetalAPI.CreateNSString(source);
        try
        {
            var library = MetalAPI.NewLibraryWithSource(
                deviceHandle, nsSource, IntPtr.Zero, out var libraryError);
            if (library == IntPtr.Zero)
            {
                var errorMsg = MetalAPI.GetNSErrorDescription(libraryError);
                Trace.WriteLine("Metal library compilation failed:");
                Trace.WriteLine(
                    errorMsg ?? ">> No error information available");
                if (libraryError != IntPtr.Zero)
                    MetalAPI.Release(libraryError);
                throw new MetalException(
                    MetalError.LibraryCompilationFailed,
                    $"Failed to compile Metal library: {errorMsg}");
            }
            return library;
        }
        finally
        {
            MetalAPI.Release(nsSource);
        }
    }

    /// <summary>
    /// Returns the Metal library handle.
    /// </summary>
    internal IntPtr LibraryPtr { get; private set; }

    /// <summary>
    /// Returns the Metal function handle.
    /// </summary>
    internal IntPtr FunctionPtr { get; private set; }

    /// <summary>
    /// Returns the Metal compute pipeline state handle.
    /// </summary>
    public IntPtr PipelineState { get; private set; }

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        if (PipelineState != IntPtr.Zero)
        {
            MetalAPI.Release(PipelineState);
            PipelineState = IntPtr.Zero;
        }

        if (FunctionPtr != IntPtr.Zero)
        {
            MetalAPI.Release(FunctionPtr);
            FunctionPtr = IntPtr.Zero;
        }

        if (LibraryPtr != IntPtr.Zero)
        {
            MetalAPI.Release(LibraryPtr);
            LibraryPtr = IntPtr.Zero;
        }
    }
}
