// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// Represents a loaded CPU kernel for vectorized CPU execution.
/// </summary>
/// <remarks>
/// CPU kernels execute via static <c>Launch()</c> methods on the compiled kernel
/// class — no native module loading is needed. This wrapper satisfies the
/// <see cref="Accelerator.LoadKernel"/> contract.
/// </remarks>
public sealed class CPUKernel : Kernel
{
    /// <summary>
    /// Creates a new CPU kernel wrapper.
    /// </summary>
    /// <param name="accelerator">The parent CPU accelerator.</param>
    /// <param name="compiledKernel">The compiled kernel metadata.</param>
    internal CPUKernel(CPUAccelerator accelerator, CPUCompiledKernel compiledKernel)
        : base(accelerator, compiledKernel)
    { }

    /// <summary>
    /// Returns the underlying CPU compiled kernel.
    /// </summary>
    public new CPUCompiledKernel CompiledKernel =>
        base.CompiledKernel.AsNotNullCast<CPUCompiledKernel>();

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing) { }
}
