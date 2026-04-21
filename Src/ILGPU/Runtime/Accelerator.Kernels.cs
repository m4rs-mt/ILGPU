// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accelerator.Kernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime;

partial class Accelerator
{
    #region Kernels

    /// <summary>
    /// Maps kernels to launch ids.
    /// </summary>
    private InlineList<Kernel> _kernels = InlineList<Kernel>.Empty;

    /// <summary>
    /// Triggers a kernel load of all registered kernels.
    /// </summary>
    private void LoadKernels()
    {
        // Collect compatible compiled kernels
        var compatible = new System.Collections.Generic.List<CompiledKernel>();
        Context.ForEachCompiledKernel(AcceleratorType, compiledKernel =>
        {
            if (compiledKernel.AcceleratorType != Device.AcceleratorType)
                return;

            // Skip incompatible kernels instead of throwing
            if (!Device.Capabilities.IsCompatible(
                compiledKernel.RequiredCapabilities))
                return;

            compatible.Add(compiledKernel);
        });

        // Sort by specificity — best match (highest ordinal) first
        compatible.Sort((a, b) =>
            b.RequiredCapabilities.SpecificityOrdinal.CompareTo(
                a.RequiredCapabilities.SpecificityOrdinal));

        // Load in sorted order
        _kernels = InlineList<Kernel>.Create(compatible.Count);
        foreach (var compiledKernel in compatible)
            LoadCompiledKernel(compiledKernel);
    }

    /// <summary>
    /// Loads a single compiled kernel and makes it available for launching.
    /// The kernel's <see cref="CompiledKernel.OnKernelLoaded"/> callback is
    /// invoked to cache the loaded runtime kernel reference.
    /// </summary>
    /// <param name="compiledKernel">The compiled kernel to load.</param>
    /// <returns>The loaded runtime kernel.</returns>
    public Kernel LoadCompiledKernel(CompiledKernel compiledKernel)
    {
        var kernel = LoadKernel(compiledKernel);
        int autoGroupSize = EstimateGroupSize(
            kernel,
            0,
            kernel.MaxNumThreadsPerGroup ?? 0,
            out int _);
        kernel.AutoGroupSize = autoGroupSize;
        compiledKernel.OnKernelLoaded(kernel);
        _kernels.Add(kernel);
        return kernel;
    }

    /// <summary>
    /// Loads the given kernel (backend-specific implementation).
    /// </summary>
    /// <param name="compiledKernel">The compiled kernel to load.</param>
    /// <returns>The loaded kernel.</returns>
    protected abstract Kernel LoadKernel(CompiledKernel compiledKernel);

    /// <summary>
    /// Launches the specified kernel by id using the stream and configuration provided.
    /// </summary>
    /// <param name="kernel">The kernel to prepare for launch.</param>
    /// <param name="kernelConfig">The kernel configuration.</param>
    /// <exception cref="InvalidOperationException">
    /// In case the specified group launch dimension is incompatible with the current
    /// accelerator.
    /// </exception>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, MustNotBeCalledByClient]
    public KernelConfig PrepareKernelLaunch(Kernel kernel, in KernelConfig kernelConfig)
    {
        // Adjust kernel launch dimensions and shared memory information
        var launchConfig = kernelConfig.WithAutoGroupSize(kernel.AutoGroupSize);

        // Check for compatibility to launch this kernel on the current accelerator
        if (launchConfig.Dimension.GroupSize > MaxNumThreadsPerGroup)
        {
            throw new InvalidOperationException(
                RuntimeErrorMessages.InvalidKernelLaunchGroupDimension);
        }

        // Bind accelerator to make sure we have a valid execution context
        Bind();

        return launchConfig;
    }

    /// <summary>
    /// Disposes all kernels.
    /// </summary>
    private void DisposeKernels_Locked()
    {
        foreach (var kernel in _kernels)
            kernel.Dispose();
        _kernels.Clear();
    }

    #endregion
}
