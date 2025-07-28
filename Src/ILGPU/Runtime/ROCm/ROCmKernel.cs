// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Text;
using static ILGPU.Runtime.ROCm.ROCmAPI;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Represents a loaded HIP/ROCm kernel backed by a hipModule and hipFunction.
/// </summary>
public sealed class ROCmKernel : Kernel
{
    /// <summary>
    /// Creates a new ROCm kernel by loading a module and resolving the function.
    /// </summary>
    /// <param name="accelerator">The parent ROCm accelerator.</param>
    /// <param name="compiledKernel">The compiled kernel to load.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal unsafe ROCmKernel(
        ROCmAccelerator accelerator,
        ROCmCompiledKernel compiledKernel)
        : base(accelerator, compiledKernel)
    {
        // Get the kernel source as bytes (HSACO binary or assembly text).
        var source = compiledKernel.GetSourceAsString();
        var sourceBytes = Encoding.UTF8.GetBytes(source + '\0');

        fixed (byte* pSource = sourceBytes)
        {
            ROCmException.ThrowIfFailed(
                CurrentAPI.ModuleLoadData(pSource, out IntPtr module));
            ModuleHandle = module;
        }

        ROCmException.ThrowIfFailed(
            CurrentAPI.ModuleGetFunction(
                ModuleHandle,
                compiledKernel.KernelName,
                out IntPtr function));
        FunctionHandle = function;
    }

    /// <summary>
    /// Returns the HIP module handle.
    /// </summary>
    public IntPtr ModuleHandle { get; private set; }

    /// <summary>
    /// Returns the HIP function handle.
    /// </summary>
    public IntPtr FunctionHandle { get; private set; }

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        if (ModuleHandle != IntPtr.Zero)
            CurrentAPI.ModuleUnload(ModuleHandle);
        ModuleHandle = IntPtr.Zero;
        FunctionHandle = IntPtr.Zero;
    }
}
