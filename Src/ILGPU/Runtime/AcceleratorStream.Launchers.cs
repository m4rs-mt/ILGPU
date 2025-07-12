// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorStream.Launchers.cs
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

partial class AcceleratorStream
{
    #region Launcher Wrappers

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch(Index1D extent, Action<Index1D> kernel) =>
        LaunchAutoGrouped(extent, index =>
        {
            // Perform automatic bounds checks
            if (index >= extent) return;

            // Invoke real kernel
            kernel((Index1D)index);
        });

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch(LongIndex1D extent, Action<LongIndex1D> kernel) =>
        LaunchAutoGrouped(extent, index =>
        {
            // Perform automatic bounds checks
            if (index >= extent) return;

            // Invoke real kernel
            kernel(index);
        });

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="kernelConfig">The thread grid configuration to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch(in KernelConfig kernelConfig, Action<KernelIndex> kernel)
    {
        var dimension = kernelConfig.Dimension;
        LaunchGrouped(kernelConfig, index =>
        {
            // Perform automatic bounds checks
            if (!dimension.IsInBounds(index)) return;

            // Invoke real kernel
            kernel(index);
        });
    }

    #endregion

    #region Multidimensional

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC. This kernel will use
    /// <see cref="Stride2D.DenseY"/> information to mimic common kernel behavior.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch2D(Index2D extent, Action<Index2D> kernel) =>
        Launch2D<Stride2D.DenseY>(extent, kernel);

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC. This kernel will use
    /// <see cref="Stride2D.DenseY"/> information to mimic common kernel behavior.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch2D(LongIndex2D extent, Action<LongIndex2D> kernel) =>
        Launch2D<Stride2D.DenseY>(extent, kernel);

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch2D<TStride>(Index2D extent, Action<Index2D> kernel)
        where TStride : unmanaged, IStride2D<TStride>
    {
        var stride = TStride.FromExtent(extent);
        Launch(extent.Size, index =>
        {
            // Reconstruct 2D index
            var index2D = stride.ReconstructFromElementIndex(index);

            // Perform automatic bounds checks
            if (Bitwise.Or(index2D.X >= extent.X, index2D.Y >= extent.Y)) return;

            // Invoke kernel function
            kernel(index2D);
        });
    }

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch2D<TStride>(LongIndex2D extent, Action<LongIndex2D> kernel)
        where TStride : unmanaged, IStride2D<TStride>
    {
        var stride = TStride.FromExtent(extent);
        Launch(extent.Size, index =>
        {
            // Reconstruct 2D index
            var index2D = stride.ReconstructFromElementIndex(index);

            // Perform automatic bounds checks
            if (Bitwise.Or(index2D.X >= extent.X, index2D.Y >= extent.Y)) return;

            // Invoke kernel function
            kernel(index2D);
        });
    }

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC. This kernel will use
    /// <see cref="Stride3D.DenseZY"/> information to mimic common kernel behavior.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch3D(Index3D extent, Action<Index3D> kernel) =>
        Launch3D<Stride3D.DenseZY>(extent, kernel);

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC. This kernel will use
    /// <see cref="Stride3D.DenseZY"/> information to mimic common kernel behavior.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch3D(LongIndex3D extent, Action<LongIndex3D> kernel) =>
        Launch3D<Stride3D.DenseZY>(extent, kernel);

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch3D<TStride>(Index3D extent, Action<Index3D> kernel)
        where TStride : unmanaged, IStride3D<TStride>
    {
        var stride = TStride.FromExtent(extent);
        Launch(extent.Size, index =>
        {
            // Reconstruct 2D index
            var index3D = stride.ReconstructFromElementIndex(index);

            // Perform automatic bounds checks
            if (Bitwise.Or(
                index3D.X >= extent.X,
                index3D.Y >= extent.Y,
                index3D.Z >= extent.Z))
            {
                return;
            }

            // Invoke kernel function
            kernel(index3D);
        });
    }

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch3D<TStride>(LongIndex3D extent, Action<LongIndex3D> kernel)
        where TStride : unmanaged, IStride3D<TStride>
    {
        var stride = TStride.FromExtent(extent);
        Launch(extent.Size, index =>
        {
            // Reconstruct 3D index
            var index3D = stride.ReconstructFromElementIndex(index);

            // Perform automatic bounds checks
            if (Bitwise.Or(
                index3D.X >= extent.X,
                index3D.Y >= extent.Y,
                index3D.Z >= extent.Z))
            {
                return;
            }

            // Invoke kernel function
            kernel(index3D);
        });
    }

    #endregion

    #region Kernel Launchers

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration]
    private void LaunchAutoGrouped(long extent, Action<long> kernel) =>
        LaunchAutoGroupedInternal(extent, (offset, index) => kernel(offset + index));

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, IsLauncher(isGrouped: false)]
    private void LaunchAutoGroupedInternal(long extent, Action<long, long> kernel) =>
        throw new NotSupportedException(ErrorMessages.NotSupportedPlatform);

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="kernelConfig">The kernel config of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration]
    private void LaunchGrouped(
        KernelConfig kernelConfig,
        Action<KernelIndex> kernel) =>
        LaunchGroupedInternal(kernelConfig, (offset, index) =>
            kernel(new(index.GridIndex + offset, index.GroupIndex)));

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="kernelConfig">The kernel config of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="NotSupportedException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, IsLauncher(isGrouped: true)]
    private void LaunchGroupedInternal(
        KernelConfig kernelConfig,
        Action<long, KernelIndex> kernel) =>
        throw new NotSupportedException(ErrorMessages.NotSupportedPlatform);

    /// <summary>
    /// Launches a kernel at runtime using the provided kernel id. This id was determined
    /// by ILGPUC during compile time.
    /// </summary>
    /// <param name="kernelId">The id of the compiled kernel to launch.</param>
    /// <param name="kernelConfig">The thread grid configuration to launch.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel, MustNotBeCalledByClient]
    public Kernel PrepareKernelLaunch(Guid kernelId, KernelConfig kernelConfig) =>
        Accelerator.AsNotNull().PrepareKernelLaunch(kernelId, kernelConfig);

    #endregion
}
