﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: KernelAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Resources;
using System;
using System.Reflection;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents an accelerator that manages typed kernel.
    /// </summary>
    /// <typeparam name="TCompiledKernel">The type of a compiled kernel.</typeparam>
    /// <typeparam name="TKernel">The type of a loaded runtime kernel</typeparam>
    public abstract class KernelAccelerator<TCompiledKernel, TKernel> : Accelerator
        where TCompiledKernel : CompiledKernel
        where TKernel : Kernel
    {
        #region Instance

        /// <summary>
        /// Constructs a new kernel accelerator.
        /// </summary>
        /// <param name="context">The target context.</param>
        /// <param name="device">The device.</param>
        protected KernelAccelerator(Context context, Device device)
            : base(context, device)
        { }

        #endregion

        #region Methods

        /// <summary cref="Accelerator.LoadKernelInternal(CompiledKernel)"/>
        protected sealed override Kernel LoadKernelInternal(CompiledKernel kernel)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (!(kernel is TCompiledKernel compiledKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);
            return CreateKernel(
                compiledKernel,
                GenerateKernelLauncherMethod(compiledKernel, 0));
        }

        /// <summary>
        /// Loads an implicitly grouped kernel on the current accelerator.
        /// </summary>
        /// <param name="kernel">The compiled kernel to load.</param>
        /// <param name="customGroupSize">The user-defined group size.</param>
        /// <param name="kernelInfo">The resolved kernel information.</param>
        /// <returns>The loaded kernel.</returns>
        protected sealed override Kernel LoadImplicitlyGroupedKernelInternal(
            CompiledKernel kernel,
            int customGroupSize,
            out KernelInfo kernelInfo)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (customGroupSize < 0 || customGroupSize > MaxNumThreadsPerGroup)
                throw new ArgumentOutOfRangeException(nameof(customGroupSize));
            if (!(kernel is TCompiledKernel compiledKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);
            if (kernel.EntryPoint.IsExplicitlyGrouped)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedExplicitlyGroupedKernel);
            }

            kernelInfo = KernelInfo.CreateFrom(
                kernel.Info,
                customGroupSize,
                null);
            return CreateKernel(
                compiledKernel,
                GenerateKernelLauncherMethod(compiledKernel, customGroupSize));
        }

        /// <summary>
        /// Loads an auto grouped kernel on the current accelerator.
        /// </summary>
        /// <param name="kernel">The compiled kernel to load.</param>
        /// <param name="kernelInfo">The resolved kernel information.</param>
        /// <returns>The loaded kernel.</returns>
        protected sealed override Kernel LoadAutoGroupedKernelInternal(
            CompiledKernel kernel,
            out KernelInfo kernelInfo)
        {
            if (kernel == null)
                throw new ArgumentNullException(nameof(kernel));
            if (!(kernel is TCompiledKernel compiledKernel))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernel);
            if (kernel.EntryPoint.IsExplicitlyGrouped)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedExplicitlyGroupedKernel);
            }

            var result = CreateKernel(compiledKernel);
            int groupSize = EstimateGroupSizeInternal(result, 0, 0, out int minGridSize);
            result.Launcher = GenerateKernelLauncherMethod(compiledKernel, groupSize);
            kernelInfo = KernelInfo.CreateFrom(
                kernel.Info,
                groupSize,
                minGridSize);
            return result;
        }

        /// <summary>
        /// Generates a dynamic kernel-launcher method that will be just-in-time compiled
        /// during the first invocation. Using the generated launcher lowers the overhead
        /// for kernel launching dramatically, since unnecessary operations (like boxing)
        /// can be avoided.
        /// </summary>
        /// <param name="kernel">The kernel to generate a launcher for.</param>
        /// <param name="customGroupSize">
        /// The custom group size used for automatic blocking.
        /// </param>
        /// <returns>The generated launcher method.</returns>
        protected abstract MethodInfo GenerateKernelLauncherMethod(
            TCompiledKernel kernel,
            int customGroupSize);

        /// <summary>
        /// Creates an abstract kernel without an initialized launcher.
        /// </summary>
        /// <param name="compiledKernel">The compiled kernel.</param>
        protected abstract TKernel CreateKernel(TCompiledKernel compiledKernel);

        /// <summary>
        /// Creates an abstract kernel with an initialized launcher.
        /// </summary>
        /// <param name="compiledKernel">The compiled kernel.</param>
        /// <param name="launcher">The actual kernel launcher method.</param>
        protected abstract TKernel CreateKernel(
            TCompiledKernel compiledKernel,
            MethodInfo launcher);

        #endregion
    }
}
