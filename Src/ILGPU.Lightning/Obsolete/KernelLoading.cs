// -----------------------------------------------------------------------------
//                                    ILGPU
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: KernelLoading.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.Runtime;
using System;
using System.Reflection;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        #region Direct Kernel Loading

        /// <summary>
        /// Loads the given kernel.
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// Hence, it has to be disposed manually.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Kernel LoadKernel(CompiledKernel kernel)
        {
            return Accelerator.LoadKernel(kernel);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// Hence, it has to be disposed manually.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadImplicitlyGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Kernel LoadImplicitlyGroupedKernel(CompiledKernel kernel, int customGroupSize)
        {
            return Accelerator.LoadImplicitlyGroupedKernel(kernel, customGroupSize);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// Hence, it has to be disposed manually.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadAutoGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Kernel LoadAutoGroupedKernel(CompiledKernel kernel)
        {
            return Accelerator.LoadAutoGroupedKernel(kernel);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// Hence, it has to be disposed manually.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadAutoGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Kernel LoadAutoGroupedKernel(
            CompiledKernel kernel,
            out int groupSize,
            out int minGridSize)
        {
            return Accelerator.LoadAutoGroupedKernel(kernel, out groupSize, out minGridSize);
        }

        #endregion

        #region Kernel Loading

        /// <summary>
        /// Loads the given kernel. Implictly-grouped kernels will be launched
        /// with a group size of the current warp size of the accelerator.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>Note that the returned kernel must not be disposed manually.</remarks>
        [Obsolete("Use Accelerator.LoadKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Kernel LoadKernel(MethodInfo method)
        {
            return Accelerator.LoadKernel(method);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel. Implictly-grouped kernel
        /// will be launched with the given group size.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>Note that the returned kernel must not be disposed manually.</remarks>
        [Obsolete("Use Accelerator.LoadImplicitlyGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Kernel LoadImplicitlyGroupedKernel(MethodInfo method, int customGroupSize)
        {
            return Accelerator.LoadImplicitlyGroupedKernel(method, customGroupSize);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>Note that the returned kernel must not be disposed manually.</remarks>
        [Obsolete("Use Accelerator.LoadAutoGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Kernel LoadAutoGroupedKernel(MethodInfo method)
        {
            return Accelerator.LoadAutoGroupedKernel(method);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>Note that the returned kernel must not be disposed manually.</remarks>
        [Obsolete("Use Accelerator.LoadAutoGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public Kernel LoadAutoGroupedKernel(MethodInfo method, out int groupSize, out int minGridSize)
        {
            return Accelerator.LoadAutoGroupedKernel(method, out groupSize, out minGridSize);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadKernel<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            return Accelerator.LoadKernel<TDelegate>(method);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that launches
        /// the loaded kernel with the default stream.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadStreamKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadStreamKernel<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            return Accelerator.LoadStreamKernel<TDelegate>(method);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that is associated
        /// with the given accelerator stream. Consequently, the resulting delegate
        /// cannot receive other accelerator streams.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="stream">The accelerator stream to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadStreamKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadStreamKernel<TDelegate>(MethodInfo method, AcceleratorStream stream)
            where TDelegate : class
        {
            return Accelerator.LoadStreamKernel<TDelegate>(method, stream);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadImplicitlyGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadImplicitlyGroupedKernel<TDelegate>(MethodInfo method, int customGroupSize)
            where TDelegate : class
        {
            return Accelerator.LoadImplicitlyGroupedKernel<TDelegate>(method, customGroupSize);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that launches
        /// the loaded kernel with the default stream.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadImplicitlyGroupedStreamKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadImplicitlyGroupedStreamKernel<TDelegate>(MethodInfo method, int customGroupSize)
            where TDelegate : class
        {
            return Accelerator.LoadImplicitlyGroupedStreamKernel<TDelegate>(method, customGroupSize);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that is associated
        /// with the given accelerator stream. Consequently, the resulting delegate
        /// cannot receive other accelerator streams.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <param name="stream">The accelerator stream to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        [Obsolete("Use Accelerator.LoadImplicitlyGroupedStreamKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadImplicitlyGroupedStreamKernel<TDelegate>(
            MethodInfo method,
            int customGroupSize,
            AcceleratorStream stream)
            where TDelegate : class
        {
            return Accelerator.LoadImplicitlyGroupedStreamKernel<TDelegate>(
                method,
                customGroupSize,
                stream);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [Obsolete("Use Accelerator.LoadAutoGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadAutoGroupedKernel<TDelegate>(
            MethodInfo method,
            out int groupSize,
            out int minGridSize)
            where TDelegate : class
        {
            return Accelerator.LoadAutoGroupedKernel<TDelegate>(
                method,
                out groupSize,
                out minGridSize);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [Obsolete("Use Accelerator.LoadAutoGroupedKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadAutoGroupedKernel<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            return Accelerator.LoadAutoGroupedKernel<TDelegate>(method);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that launches
        /// the loaded kernel with the default stream.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [Obsolete("Use Accelerator.LoadAutoGroupedStreamKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadAutoGroupedStreamKernel<TDelegate>(
            MethodInfo method,
            out int groupSize,
            out int minGridSize)
            where TDelegate : class
        {
            return Accelerator.LoadAutoGroupedStreamKernel<TDelegate>(
                method,
                out groupSize,
                out minGridSize);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that launches
        /// the loaded kernel with the default stream.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [Obsolete("Use Accelerator.LoadAutoGroupedStreamKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadAutoGroupedStreamKernel<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            return Accelerator.LoadAutoGroupedStreamKernel<TDelegate>(method);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that is associated
        /// with the given accelerator stream. Consequently, the resulting delegate
        /// cannot receive other accelerator streams.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="stream">The accelerator stream to use.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [Obsolete("Use Accelerator.LoadAutoGroupedStreamKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadAutoGroupedStreamKernel<TDelegate>(
            MethodInfo method,
            AcceleratorStream stream,
            out int groupSize,
            out int minGridSize)
            where TDelegate : class
        {
            return Accelerator.LoadAutoGroupedStreamKernel<TDelegate>(
                method,
                stream,
                out groupSize,
                out minGridSize);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that is associated
        /// with the given accelerator stream. Consequently, the resulting delegate
        /// cannot receive other accelerator streams.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="stream">The accelerator stream to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [Obsolete("Use Accelerator.LoadAutoGroupedStreamKernel. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public TDelegate LoadAutoGroupedStreamKernel<TDelegate>(MethodInfo method, AcceleratorStream stream)
            where TDelegate : class
        {
            return Accelerator.LoadAutoGroupedStreamKernel<TDelegate>(method, stream);
        }

        #endregion
    }
}
