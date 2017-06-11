// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: KernelLoader.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Compiler;
using ILGPU.Runtime;
using System;
using System.Reflection;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        #region Loader Types

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

        #endregion

        #region Methods

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
