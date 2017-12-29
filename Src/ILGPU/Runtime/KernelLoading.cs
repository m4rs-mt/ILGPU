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
        /// </remarks>
        public Kernel LoadKernel(CompiledKernel kernel)
        {
            Bind(); return LoadKernelInternal(kernel);
        }

        /// <summary>
        /// Loads the given kernel.
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// </remarks>
        protected abstract Kernel LoadKernelInternal(CompiledKernel kernel);

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
        /// </remarks>
        public Kernel LoadImplicitlyGroupedKernel(CompiledKernel kernel, int customGroupSize)
        {
            Bind(); return LoadImplicitlyGroupedKernelInternal(kernel, customGroupSize);
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
        /// </remarks>
        protected abstract Kernel LoadImplicitlyGroupedKernelInternal(
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
        /// </remarks>
        public Kernel LoadAutoGroupedKernel(
            CompiledKernel kernel,
            out int groupSize,
            out int minGridSize)
        {
            Bind(); return LoadAutoGroupedKernelInternal(kernel, out groupSize, out minGridSize);
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
        /// </remarks>
        protected abstract Kernel LoadAutoGroupedKernelInternal(
            CompiledKernel kernel,
            out int groupSize,
            out int minGridSize);

        #endregion

        #region Generic Kernel Loading

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
            public static readonly DefaultKernelLoader Default = new DefaultKernelLoader()
            {
                GroupSize = 0,
                MinGridSize = 0,
            };

            /// <summary cref="IKernelLoader.GroupSize"/>
            public int GroupSize { get; set; }

            /// <summary cref="IKernelLoader.MinGridSize"/>
            public int MinGridSize { get; set; }

            /// <summary cref="IKernelLoader.LoadKernel(Accelerator, CompiledKernel)"/>
            public Kernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel)
            {
                return accelerator.LoadKernel(compiledKernel);
            }
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
                MinGridSize = 0;
            }

            /// <summary cref="IKernelLoader.GroupSize"/>
            public int GroupSize { get; set; }

            /// <summary cref="IKernelLoader.MinGridSize"/>
            public int MinGridSize { get; set; }

            /// <summary cref="IKernelLoader.LoadKernel(Accelerator, CompiledKernel)"/>
            public Kernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel)
            {
                return accelerator.LoadImplicitlyGroupedKernel(compiledKernel, GroupSize);
            }
        }

        /// <summary>
        /// Represents an automatically configured grouped kernel loader for implicitly-grouped kernels.
        /// </summary>
        private struct AutoKernelLoader : IKernelLoader
        {
            public static readonly AutoKernelLoader Default = new AutoKernelLoader()
            {
                GroupSize = -1,
                MinGridSize = -1,
            };

            /// <summary cref="IKernelLoader.GroupSize"/>
            public int GroupSize { get; set; }

            /// <summary cref="IKernelLoader.MinGridSize"/>
            public int MinGridSize { get; set; }

            /// <summary cref="IKernelLoader.LoadKernel(Accelerator, CompiledKernel)"/>
            public Kernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel)
            {
                var result = accelerator.LoadAutoGroupedKernel(
                    compiledKernel,
                    out int groupSize,
                    out int minGridSize);
                GroupSize = groupSize;
                MinGridSize = minGridSize;
                return result;
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

        /// <summary>
        /// Loads a kernel specified by the given method and returns a launcher of the specified type.
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <typeparam name="TKernelLoader">The type of the custom kernel loader.</typeparam>
        /// <typeparam name="TLauncherProvider">The type of the custom launcher provider.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <param name="launcherProvider">The launcher provider.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TDelegate LoadGenericKernel<TDelegate, TKernelLoader, TLauncherProvider>(
            MethodInfo method,
            ref TKernelLoader kernelLoader,
            ref TLauncherProvider launcherProvider)
            where TDelegate : class
            where TKernelLoader : struct, IKernelLoader
            where TLauncherProvider : struct, ILauncherProvider
        {
            var kernel = LoadGenericKernel(method, ref kernelLoader);
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
            var loader = DefaultKernelLoader.Default;
            return LoadGenericKernel(method, ref loader);
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
            return LoadGenericKernel(method, ref loader);
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
            var loader = AutoKernelLoader.Default;
            return LoadGenericKernel(method, ref loader);
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
            var loader = AutoKernelLoader.Default;
            var result = LoadGenericKernel(method, ref loader);
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
            var loader = DefaultKernelLoader.Default;
            var launcher = new LauncherProvider();
            return LoadGenericKernel<TDelegate, DefaultKernelLoader, LauncherProvider>(
                method,
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
            var loader = DefaultKernelLoader.Default;
            var launcher = new DefaultStreamLauncherProvider();
            return LoadGenericKernel<TDelegate, DefaultKernelLoader, DefaultStreamLauncherProvider>(
                method,
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
            var loader = DefaultKernelLoader.Default;
            var launcher = new StreamLauncherProvider(stream);
            return LoadGenericKernel<TDelegate, DefaultKernelLoader, StreamLauncherProvider>(
                method,
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
            var loader = AutoKernelLoader.Default;
            var launcher = new LauncherProvider();
            var result = LoadGenericKernel<TDelegate, AutoKernelLoader, LauncherProvider>(
                method,
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
            var loader = AutoKernelLoader.Default;
            var launcher = new LauncherProvider();
            return LoadGenericKernel<TDelegate, AutoKernelLoader, LauncherProvider>(
                method,
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
            var loader = AutoKernelLoader.Default;
            var launcher = new DefaultStreamLauncherProvider();
            var result = LoadGenericKernel<TDelegate, AutoKernelLoader, DefaultStreamLauncherProvider>(
                method,
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
            var loader = AutoKernelLoader.Default;
            var launcher = new DefaultStreamLauncherProvider();
            return LoadGenericKernel<TDelegate, AutoKernelLoader, DefaultStreamLauncherProvider>(
                method,
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
            var loader = AutoKernelLoader.Default;
            var launcher = new StreamLauncherProvider(stream);
            var result = LoadGenericKernel<TDelegate, AutoKernelLoader, StreamLauncherProvider>(
                method,
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
            var loader = AutoKernelLoader.Default;
            var launcher = new StreamLauncherProvider(stream);
            return LoadGenericKernel<TDelegate, AutoKernelLoader, StreamLauncherProvider>(
                method,
                ref loader,
                ref launcher);
        }

        #endregion
    }
}
