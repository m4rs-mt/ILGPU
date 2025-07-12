// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accelerator.Kernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime;

partial class Accelerator
{
    #region Kernels

    /// <summary>
    /// Maps kernels to launch ids.
    /// </summary>
    private readonly Dictionary<Guid, (Kernel Kernel, int AutoGroupSize)> _kernels;

    /// <summary>
    /// Triggers a kernel load of all kernels.
    /// </summary>
    private void LoadKernels() =>
        Context.ForEachCompiledKernel((compiledKernel, index) =>
        {
            var kernel = LoadKernel(compiledKernel);
            int autoGroupSize = EstimateGroupSize(
                kernel,
                0,
                kernel.MaxNumThreadsPerGroup ?? 0,
                out int _);
            _kernels.Add(compiledKernel.Guid, (kernel, autoGroupSize));
        });

    /// <summary>
    /// Loads the given kernel.
    /// </summary>
    /// <param name="compiledKernel">The compiled kernel to load.</param>
    /// <returns>The loaded kernel.</returns>
    protected abstract Kernel LoadKernel(CompiledKernel compiledKernel);

    /// <summary>
    /// Launches the specified kernel by id using the stream and configuration provided.
    /// </summary>
    /// <param name="kernelId">The kernel id pointing to the kernel to launch.</param>
    /// <param name="kernelConfig">The kernel configuration.</param>
    /// <exception cref="InvalidOperationException">
    /// In case the specified group launch dimension is incompatible with the current
    /// accelerator.
    /// </exception>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, MustNotBeCalledByClient]
    public Kernel PrepareKernelLaunch(Guid kernelId, in KernelConfig kernelConfig)
    {
        // Get the kernel to launch
        var (kernel, autoGroupSize) = _kernels[kernelId];

        // Adjust kernel launch dimensions and shared memory information
        var launchConfig = kernelConfig.WithAutoGroupSize(autoGroupSize);

        // Check for compatibility to launch this kernel on the current accelerator
        if (launchConfig.Dimension.GroupSize > MaxNumThreadsPerGroup)
        {
            throw new InvalidOperationException(
                RuntimeErrorMessages.InvalidKernelLaunchGroupDimension);
        }

        // Bind accelerator to make sure we have a valid execution context
        Bind();

        // Launch kernel
        return kernel;
    }

    /// <summary>
    /// Disposes all kernels.
    /// </summary>
    private void DisposeKernels_Locked()
    {
        foreach (var (kernel, _) in _kernels.Values)
            kernel.Dispose();
        _kernels.Clear();
    }

    #endregion
}
