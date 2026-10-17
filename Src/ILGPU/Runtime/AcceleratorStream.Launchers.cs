// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorStream.Launchers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ILGPU.CodeGeneration;
using ILGPU.Resources;
using ILGPU.Util;

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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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

    /// <summary>
    /// Triggers a 2D kernel launch. Delegates to
    /// <see cref="Launch2D{TStride}(Index2D, Action{Index2D})"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch(Index2D extent, Action<Index2D> kernel) =>
        Launch2D(extent, kernel);

    /// <summary>
    /// Triggers a 3D kernel launch. Delegates to
    /// <see cref="Launch3D(Index3D, Action{Index3D})"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, DelayCodeGeneration, ReplaceWithLauncher]
    public void Launch(Index3D extent, Action<Index3D> kernel) =>
        Launch3D(extent, kernel);

    #region Multidimensional

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC. This kernel will use
    /// <see cref="Stride2D.DenseY"/> information to mimic common kernel behavior.
    /// </summary>
    /// <param name="extent">The extent of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, IsLauncher(isGrouped: false)]
    private void LaunchAutoGroupedInternal(long extent, Action<long, long> kernel) =>
        throw CreateKernelNotCompiledException(kernel);

    /// <summary>
    /// Triggers a kernel launch on the current accelerator stream. The lambda function
    /// provided will be compiled with ILGPUC.
    /// </summary>
    /// <param name="kernelConfig">The kernel config of the thread grid to launch.</param>
    /// <param name="kernel">The kernel to launch</param>
    /// <exception cref="KernelNotCompiledException">
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
    /// <exception cref="KernelNotCompiledException">
    /// This method cannot be called directly at runtime. The kernel provided must be
    /// compiled with ILGPUC first.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [NotInsideKernel, IsLauncher(isGrouped: true)]
    private void LaunchGroupedInternal(
        KernelConfig kernelConfig,
        Action<long, KernelIndex> kernel) =>
        throw CreateKernelNotCompiledException(kernel);

    /// <summary>
    /// Creates the exception reported by the runtime launchers: they are reached
    /// only if ILGPUC did not compile the launch site of the kernel.
    /// </summary>
    /// <param name="kernel">The kernel passed to the runtime launcher.</param>
    private KernelNotCompiledException CreateKernelNotCompiledException(
        Delegate kernel) =>
        new(
            GetKernelName(kernel),
            AcceleratorType,
            RuntimeErrorMessages.KernelNotCompiledLaunchSite);

    /// <summary>
    /// Returns a readable name of the kernel launched by the user. The launcher
    /// wrappers of this class capture the user kernel in closures, which are
    /// unwrapped to name the user kernel instead of a wrapper.
    /// </summary>
    /// <param name="kernel">The kernel passed to a runtime launcher.</param>
    /// <returns>The kernel name in the form Type.Method.</returns>
    private static string GetKernelName(Delegate kernel)
    {
        var current = kernel;
        while (current.Target is { } target &&
            target.GetType().DeclaringType == typeof(AcceleratorStream))
        {
            Delegate? captured = null;
            foreach (var field in target.GetType().GetFields())
            {
                if (field.GetValue(target) is Delegate capturedKernel)
                {
                    captured = capturedKernel;
                    break;
                }
            }
            if (captured is null)
                break;
            current = captured;
        }

        // Lambdas and local functions are named <Method>b__0_0 or
        // <Method>g__Local|0_0 and live in compiler-generated closure classes
        var method = current.Method;
        var methodName = method.Name;
        int nameEnd = methodName.IndexOf('>', StringComparison.Ordinal);
        if (methodName.StartsWith('<') && nameEnd > 1)
            methodName = methodName[1..nameEnd];

        var type = method.DeclaringType;
        while (type?.DeclaringType is { } declaringType &&
            type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        {
            type = declaringType;
        }
        return type is null ? methodName : $"{GetTypeName(type)}.{methodName}";
    }

    /// <summary>
    /// Returns the namespace-qualified name of the given type without generic
    /// arity suffixes, using dots to separate nested types.
    /// </summary>
    /// <param name="type">The type.</param>
    private static string GetTypeName(Type type)
    {
        var name = type.Name;
        int arityStart = name.IndexOf('`', StringComparison.Ordinal);
        if (arityStart >= 0)
            name = name[..arityStart];

        if (type.DeclaringType is { } declaringType)
            return $"{GetTypeName(declaringType)}.{name}";
        return string.IsNullOrEmpty(type.Namespace) ? name : $"{type.Namespace}.{name}";
    }

    /// <summary>
    /// Prepares a kernel launch at runtime.
    /// </summary>
    /// <param name="kernel">The kernel to launch.</param>
    /// <param name="kernelConfig">The thread grid configuration to launch.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel, MustNotBeCalledByClient]
    public KernelConfig PrepareKernelLaunch(
        Kernel kernel,
        in KernelConfig kernelConfig) =>
        Accelerator.AsNotNull().PrepareKernelLaunch(kernel, kernelConfig);

    /// <summary>
    /// Verifies that a kernel compiled by ILGPUC for the given accelerator type can
    /// be launched on this stream.
    /// </summary>
    /// <param name="kernelName">The name of the compiled kernel.</param>
    /// <param name="acceleratorType">
    /// The accelerator type the kernel was compiled for.
    /// </param>
    /// <exception cref="KernelNotCompiledException">
    /// The kernel was compiled for a different accelerator type.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel, MustNotBeCalledByClient]
    public void VerifyCompiledKernel(string kernelName, AcceleratorType acceleratorType)
    {
        if (AcceleratorType != acceleratorType)
            ThrowKernelNotCompiled(kernelName, acceleratorType);
    }

    /// <summary>
    /// Returns the runtime kernel of a kernel compiled by ILGPUC for the given
    /// accelerator type to launch it on this stream.
    /// </summary>
    /// <typeparam name="TKernel">The accelerator-specific kernel type.</typeparam>
    /// <param name="kernel">The loaded runtime kernel, if any.</param>
    /// <param name="kernelName">The name of the compiled kernel.</param>
    /// <param name="acceleratorType">
    /// The accelerator type the kernel was compiled for.
    /// </param>
    /// <returns>The runtime kernel to launch.</returns>
    /// <exception cref="KernelNotCompiledException">
    /// The kernel was compiled for a different accelerator type.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The kernel is not loaded on the accelerator of this stream.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel, MustNotBeCalledByClient]
    public TKernel GetCompiledKernel<TKernel>(
        Kernel? kernel,
        string kernelName,
        AcceleratorType acceleratorType)
        where TKernel : Kernel
    {
        VerifyCompiledKernel(kernelName, acceleratorType);
        if (kernel is not TKernel typedKernel)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                RuntimeErrorMessages.KernelNotLoaded,
                kernelName,
                AcceleratorType));
        }
        return typedKernel;
    }

    /// <summary>
    /// Creates the exception reporting that a generic kernel compiled by ILGPUC is
    /// launched with type arguments it was not compiled for.
    /// </summary>
    /// <param name="kernelName">
    /// The name of the generic kernel including its type arguments.
    /// </param>
    /// <returns>The exception to throw.</returns>
    [NotInsideKernel, MustNotBeCalledByClient]
    public KernelNotCompiledException CreateKernelNotSpecializedException(
        string kernelName) =>
        new(
            kernelName,
            AcceleratorType,
            RuntimeErrorMessages.KernelNotCompiledTypeArguments);

    /// <summary>
    /// Throws the exception reporting that a kernel compiled for another accelerator
    /// type was launched on this stream.
    /// </summary>
    /// <param name="kernelName">The name of the compiled kernel.</param>
    /// <param name="acceleratorType">
    /// The accelerator type the kernel was compiled for.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowKernelNotCompiled(
        string kernelName,
        AcceleratorType acceleratorType) =>
        throw new KernelNotCompiledException(
            kernelName,
            AcceleratorType,
            string.Format(
                CultureInfo.CurrentCulture,
                RuntimeErrorMessages.KernelNotCompiledOtherAccelerator,
                acceleratorType,
                AcceleratorType));

    #endregion
}
