// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelLoading.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    partial class Accelerator
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
        public abstract Kernel LoadKernel(CompiledKernel kernel);

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
        public abstract Kernel LoadImplicitlyGroupedKernel(
            CompiledKernel kernel,
            int customGroupSize);

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
        public Kernel LoadAutoGroupedKernel(CompiledKernel kernel)
        {
            return LoadAutoGroupedKernel(kernel, out int groupSize, out int minGridSize);
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
        public abstract Kernel LoadAutoGroupedKernel(
            CompiledKernel kernel,
            out int groupSize,
            out int minGridSize);

        #endregion

        #region Generic Kernel Loading

        /// <summary>
        /// Represents a generic kernel loader.
        /// </summary>
        private interface IKernelLoader
        {
            /// <summary>
            /// Loads the given kernel using the given accelerator.
            /// </summary>
            /// <param name="accelerator">The target accelerator for the loading operation.</param>
            /// <param name="compiledKernel">The compiled kernel to load.</param>
            /// <returns>The loaded kernel.</returns>
            CachedKernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel);

            /// <summary>
            /// Fetches information from the given cached kernel.
            /// </summary>
            /// <param name="cachedKernel">The cached kernel.</param>
            void FetchInformation(CachedKernel cachedKernel);
        }

        /// <summary>
        /// Represents a launcher provider to create launcher delegates.
        /// </summary>
        private interface ILauncherProvider
        {
            /// <summary>
            /// Creates a launcher delegate for the given kernel.
            /// </summary>
            /// <typeparam name="TDelegate">The delegate type.</typeparam>
            /// <param name="kernel">The kernel for the creation operation.</param>
            /// <returns>A launcher delegate for the given kernel.</returns>
            TDelegate CreateLauncher<TDelegate>(Kernel kernel)
                where TDelegate : class;
        }

        /// <summary>
        /// Represents a default kernel loader.
        /// </summary>
        private struct DefaultKernelLoader : IKernelLoader
        {

            /// <summary cref="IKernelLoader.LoadKernel(Accelerator, CompiledKernel)"/>
            public CachedKernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel)
            {
                return new CachedKernel(accelerator.LoadKernel(compiledKernel));
            }

            /// <summary cref="IKernelLoader.FetchInformation(CachedKernel)"/>
            public void FetchInformation(CachedKernel cachedKernel)
            { }
        }

        /// <summary>
        /// Represents a grouped kernel loader for implicitly-grouped kernels.
        /// </summary>
        private struct GroupedKernelLoader : IKernelLoader
        {
            /// <summary>
            /// Constructs a new grouped kernel loader.
            /// </summary>
            /// <param name="groupSize">The custom group size.</param>
            public GroupedKernelLoader(int groupSize)
            {
                GroupSize = groupSize;
            }

            /// <summary>
            /// Returns the custom group size.
            /// </summary>
            public int GroupSize { get; private set; }

            /// <summary cref="IKernelLoader.LoadKernel(Accelerator, CompiledKernel)"/>
            public CachedKernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel)
            {
                return new CachedKernel(
                    accelerator.LoadImplicitlyGroupedKernel(compiledKernel, GroupSize),
                    GroupSize,
                    0);
            }

            /// <summary cref="IKernelLoader.FetchInformation(CachedKernel)"/>
            public void FetchInformation(CachedKernel cachedKernel)
            {
                GroupSize = cachedKernel.GroupSize;
            }
        }

        /// <summary>
        /// Represents an automatically configured grouped kernel loader for implicitly-grouped kernels.
        /// </summary>
        private struct AutoKernelLoader : IKernelLoader
        {
            private int groupSize;
            private int minGridSize;

            /// <summary>
            /// Returns the computed group size.
            /// </summary>
            public int GroupSize => groupSize;

            /// <summary>
            /// Returns the computed minumum grid size.
            /// </summary>
            public int MinGridSize => minGridSize;

            /// <summary cref="IKernelLoader.LoadKernel(Accelerator, CompiledKernel)"/>
            public CachedKernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel)
            {
                var kernel = accelerator.LoadAutoGroupedKernel(compiledKernel, out groupSize, out minGridSize);
                return new CachedKernel(kernel, groupSize, minGridSize);
            }

            /// <summary cref="IKernelLoader.FetchInformation(CachedKernel)"/>
            public void FetchInformation(CachedKernel cachedKernel)
            {
                groupSize = cachedKernel.GroupSize;
                minGridSize = cachedKernel.MinGridSize;
            }
        }

        /// <summary>
        /// Represents a default launcher provider for kernels.
        /// </summary>
        private struct DefaultStreamLauncherProvider : ILauncherProvider
        {
            /// <summary cref="ILauncherProvider.CreateLauncher{TDelegate}(Kernel)"/>
            public TDelegate CreateLauncher<TDelegate>(Kernel kernel)
                where TDelegate : class
            {
                return kernel.CreateStreamLauncherDelegate<TDelegate>();
            }
        }

        /// <summary>
        /// Represents an implicit-stream-launcher provider for kernels.
        /// </summary>
        private struct StreamLauncherProvider : ILauncherProvider
        {
            /// <summary>
            /// Constructs a new stream-launcher provider.
            /// </summary>
            /// <param name="stream">The associated accelerator stream.</param>
            public StreamLauncherProvider(AcceleratorStream stream)
            {
                Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            }

            /// <summary>
            /// Returns the associated accelerator stream.
            /// </summary>
            public AcceleratorStream Stream { get; }

            /// <summary cref="ILauncherProvider.CreateLauncher{TDelegate}(Kernel)"/>
            public TDelegate CreateLauncher<TDelegate>(Kernel kernel)
                where TDelegate : class
            {
                return kernel.CreateStreamLauncherDelegate<TDelegate>(Stream);
            }
        }

        /// <summary>
        /// Represents a launcher provider for kernels.
        /// </summary>
        private struct LauncherProvider : ILauncherProvider
        {
            /// <summary cref="ILauncherProvider.CreateLauncher{TDelegate}(Kernel)"/>
            public TDelegate CreateLauncher<TDelegate>(Kernel kernel)
                where TDelegate : class
            {
                return kernel.CreateLauncherDelegate<TDelegate>();
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Dictionary<CachedKernelKey, CachedKernel> kernelCache =
            new Dictionary<CachedKernelKey, CachedKernel>();

        /// <summary>
        /// Loads a kernel specified by the given method.
        /// </summary>
        /// <typeparam name="TKernelLoader">The type of the custom kernel loader.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The custom group size for implicitly-grouped kernels (if any).</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <returns>The loaded kernel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Kernel LoadGenericKernel<TKernelLoader>(
            MethodInfo method,
            int? groupSize,
            ref TKernelLoader kernelLoader)
            where TKernelLoader : struct, IKernelLoader
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            var cachedKey = new CachedKernelKey(method, groupSize);
            if (kernelCache.TryGetValue(cachedKey, out CachedKernel result))
            {
                kernelLoader.FetchInformation(result);
                return result.Kernel;
            }
            var compiledKernel = CompileKernel(method);
            var kernel = kernelLoader.LoadKernel(this, compiledKernel);
            kernelCache.Add(cachedKey, kernel);
            return kernel.Kernel;
        }

        /// <summary>
        /// Loads a kernel specified by the given method and returns a launcher of the specified type.
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <typeparam name="TKernelLoader">The type of the custom kernel loader.</typeparam>
        /// <typeparam name="TLauncherProvider">The type of the custom launcher provider.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The custom group size for implicitly-grouped kernels (if any).</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <param name="launcherProvider">The launcher provider.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TDelegate LoadGenericKernel<TDelegate, TKernelLoader, TLauncherProvider>(
            MethodInfo method,
            int? groupSize,
            ref TKernelLoader kernelLoader,
            ref TLauncherProvider launcherProvider)
            where TDelegate : class
            where TKernelLoader : struct, IKernelLoader
            where TLauncherProvider : struct, ILauncherProvider
        {
            var kernel = LoadGenericKernel(method, groupSize, ref kernelLoader);
            return launcherProvider.CreateLauncher<TDelegate>(kernel);
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
        public Kernel LoadKernel(MethodInfo method)
        {
            var loader = new DefaultKernelLoader();
            return LoadGenericKernel(method, 0, ref loader);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel. Implictly-grouped kernel
        /// will be launched with the given group size.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>Note that the returned kernel must not be disposed manually.</remarks>
        public Kernel LoadImplicitlyGroupedKernel(MethodInfo method, int customGroupSize)
        {
            var loader = new GroupedKernelLoader(customGroupSize);
            return LoadGenericKernel(method, customGroupSize, ref loader);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>Note that the returned kernel must not be disposed manually.</remarks>
        public Kernel LoadAutoGroupedKernel(MethodInfo method)
        {
            var loader = new AutoKernelLoader();
            return LoadGenericKernel(method, null, ref loader);
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
        public Kernel LoadAutoGroupedKernel(MethodInfo method, out int groupSize, out int minGridSize)
        {
            var loader = new AutoKernelLoader();
            var result = LoadGenericKernel(method, null, ref loader);
            groupSize = loader.GroupSize;
            minGridSize = loader.MinGridSize;
            return result;
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
        public TDelegate LoadKernel<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            var loader = new DefaultKernelLoader();
            var launcher = new LauncherProvider();
            return LoadGenericKernel<TDelegate, DefaultKernelLoader, LauncherProvider>(
                method,
                0,
                ref loader,
                ref launcher);
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
        public TDelegate LoadStreamKernel<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            var loader = new DefaultKernelLoader();
            var launcher = new DefaultStreamLauncherProvider();
            return LoadGenericKernel<TDelegate, DefaultKernelLoader, DefaultStreamLauncherProvider>(
                method,
                0,
                ref loader,
                ref launcher);
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
        public TDelegate LoadStreamKernel<TDelegate>(MethodInfo method, AcceleratorStream stream)
            where TDelegate : class
        {
            var loader = new DefaultKernelLoader();
            var launcher = new StreamLauncherProvider(stream);
            return LoadGenericKernel<TDelegate, DefaultKernelLoader, StreamLauncherProvider>(
                method,
                0,
                ref loader,
                ref launcher);
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
        public TDelegate LoadImplicitlyGroupedKernel<TDelegate>(MethodInfo method, int customGroupSize)
            where TDelegate : class
        {
            var loader = new GroupedKernelLoader(customGroupSize);
            var launcher = new LauncherProvider();
            return LoadGenericKernel<TDelegate, GroupedKernelLoader, LauncherProvider>(
                method,
                customGroupSize,
                ref loader,
                ref launcher);
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
        public TDelegate LoadImplicitlyGroupedStreamKernel<TDelegate>(MethodInfo method, int customGroupSize)
            where TDelegate : class
        {
            var loader = new GroupedKernelLoader(customGroupSize);
            var launcher = new DefaultStreamLauncherProvider();
            return LoadGenericKernel<TDelegate, GroupedKernelLoader, DefaultStreamLauncherProvider>(
                method,
                customGroupSize,
                ref loader,
                ref launcher);
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
        public TDelegate LoadImplicitlyGroupedStreamKernel<TDelegate>(
            MethodInfo method,
            int customGroupSize,
            AcceleratorStream stream)
            where TDelegate : class
        {
            var loader = new GroupedKernelLoader(customGroupSize);
            var launcher = new StreamLauncherProvider(stream);
            return LoadGenericKernel<TDelegate, GroupedKernelLoader, StreamLauncherProvider>(
                method,
                customGroupSize,
                ref loader,
                ref launcher);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedKernel<TDelegate>(
            MethodInfo method,
            out int groupSize,
            out int minGridSize)
            where TDelegate : class
        {
            var loader = new AutoKernelLoader();
            var launcher = new LauncherProvider();
            var result = LoadGenericKernel<TDelegate, AutoKernelLoader, LauncherProvider>(
                method,
                null,
                ref loader,
                ref launcher);
            groupSize = loader.GroupSize;
            minGridSize = loader.MinGridSize;
            return result;
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedKernel<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            var loader = new AutoKernelLoader();
            var launcher = new LauncherProvider();
            return LoadGenericKernel<TDelegate, AutoKernelLoader, LauncherProvider>(
                method,
                null,
                ref loader,
                ref launcher);
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that launches
        /// the loaded kernel with the default stream.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedStreamKernel<TDelegate>(
            MethodInfo method,
            out int groupSize,
            out int minGridSize)
            where TDelegate : class
        {
            var loader = new AutoKernelLoader();
            var launcher = new DefaultStreamLauncherProvider();
            var result = LoadGenericKernel<TDelegate, AutoKernelLoader, DefaultStreamLauncherProvider>(
                method,
                null,
                ref loader,
                ref launcher);
            groupSize = loader.GroupSize;
            minGridSize = loader.MinGridSize;
            return result;
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that launches
        /// the loaded kernel with the default stream.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedStreamKernel<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            var loader = new AutoKernelLoader();
            var launcher = new DefaultStreamLauncherProvider();
            return LoadGenericKernel<TDelegate, AutoKernelLoader, DefaultStreamLauncherProvider>(
                method,
                null,
                ref loader,
                ref launcher);
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
        public TDelegate LoadAutoGroupedStreamKernel<TDelegate>(
            MethodInfo method,
            AcceleratorStream stream,
            out int groupSize,
            out int minGridSize)
            where TDelegate : class
        {
            var loader = new AutoKernelLoader();
            var launcher = new StreamLauncherProvider(stream);
            var result = LoadGenericKernel<TDelegate, AutoKernelLoader, StreamLauncherProvider>(
                method,
                null,
                ref loader,
                ref launcher);
            groupSize = loader.GroupSize;
            minGridSize = loader.MinGridSize;
            return result;
        }

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that is associated
        /// with the given accelerator stream. Consequently, the resulting delegate
        /// cannot receive other accelerator streams.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="stream">The accelerator stream to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedStreamKernel<TDelegate>(MethodInfo method, AcceleratorStream stream)
            where TDelegate : class
        {
            var loader = new AutoKernelLoader();
            var launcher = new StreamLauncherProvider(stream);
            return LoadGenericKernel<TDelegate, AutoKernelLoader, StreamLauncherProvider>(
                method,
                null,
                ref loader,
                ref launcher);
        }

        #endregion
    }
}
