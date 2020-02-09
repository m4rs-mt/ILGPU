// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelLoading.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.EntryPoints;
using ILGPU.Resources;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Kernel Creation

        /// <summary>
        /// Adjusts and verifies a customm group size of a specific kernel.
        /// Note that this function ensures that implicitly grouped kernels
        /// without an explicit group size will be launched with a group size
        /// that is equal to the available warp size.
        /// </summary>
        /// <param name="customGroupSize">The custom group size to adjust and verify.</param>
        /// <param name="entryPoint">The kernel entry point.</param>
        internal void AdjustAndVerifyKernelGroupSize(
            ref int customGroupSize,
            EntryPoint entryPoint)
        {
            if (customGroupSize < 0)
                throw new ArgumentOutOfRangeException(nameof(customGroupSize));

            if (entryPoint.IsExplicitlyGrouped)
            {
                if (customGroupSize > 0)
                    throw new InvalidOperationException(RuntimeErrorMessages.InvalidCustomGroupSize);
            }
            else if (customGroupSize == 0)
                customGroupSize = WarpSize;

            var maxNumThreadsPerGroup = entryPoint.Specialization.MaxNumThreadsPerGroup;
            if (maxNumThreadsPerGroup.HasValue && customGroupSize > maxNumThreadsPerGroup.Value)
                throw new InvalidOperationException(RuntimeErrorMessages.InvalidKernelSpecializationGroupSize);
        }

        #endregion

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
        public Kernel LoadAutoGroupedKernel(CompiledKernel kernel) =>
            LoadAutoGroupedKernel(kernel, out var _, out var _);

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
            public Kernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel) =>
                accelerator.LoadKernel(compiledKernel);
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
            public Kernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel) =>
                accelerator.LoadImplicitlyGroupedKernel(compiledKernel, GroupSize);
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
        /// Loads a kernel specified by the given method and returns a launcher of the specified type.
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <typeparam name="TKernelLoader">The type of the custom kernel loader.</typeparam>
        /// <param name="entry">The entry point to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TDelegate LoadGenericKernel<TDelegate, TKernelLoader>(
            EntryPointDescription entry,
            KernelSpecialization specialization,
            ref TKernelLoader kernelLoader)
            where TDelegate : Delegate
            where TKernelLoader : struct, IKernelLoader
        {
            var kernel = LoadGenericKernel(entry, specialization, ref kernelLoader);
            return kernel.CreateLauncherDelegate<TDelegate>();
        }

        #endregion

        #region Kernel Loading

        /// <summary>
        /// Loads the given explicitly grouped kernel. Implictly-grouped kernels are not supported.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>Note that the returned kernel must not be disposed manually.</remarks>
        public Kernel LoadKernel(MethodInfo method) =>
            LoadKernel(method, KernelSpecialization.Empty);

        /// <summary>
        /// Loads the given explicitly grouped kernel. Implictly-grouped kernels are not supported.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>Note that the returned kernel must not be disposed manually.</remarks>
        public Kernel LoadKernel(MethodInfo method, KernelSpecialization specialization)
        {
            var loader = DefaultKernelLoader.Default;
            return LoadGenericKernel(
                EntryPointDescription.FromExplicitlyGroupedKernel(method),
                specialization,
                ref loader);
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
            return LoadGenericKernel(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                new KernelSpecialization(customGroupSize, null),
                ref loader);
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
            return LoadGenericKernel(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                KernelSpecialization.Empty,
                ref loader);
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
            var result = LoadGenericKernel(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                KernelSpecialization.Empty,
                ref loader);
            groupSize = loader.GroupSize;
            minGridSize = loader.MinGridSize;
            return result;
        }

        /// <summary>
        /// Loads the given explicitly grouped kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernels are not supported.
        /// </remarks>
        public TDelegate LoadKernel<TDelegate>(MethodInfo method)
            where TDelegate : Delegate =>
            LoadKernel<TDelegate>(method, KernelSpecialization.Empty);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        public TDelegate LoadKernel<TDelegate>(MethodInfo method, KernelSpecialization specialization)
            where TDelegate : Delegate
        {
            var loader = DefaultKernelLoader.Default;
            return LoadGenericKernel<TDelegate, DefaultKernelLoader>(
                EntryPointDescription.FromExplicitlyGroupedKernel(method),
                specialization,
                ref loader);
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
            where TDelegate : Delegate
        {
            var loader = new GroupedKernelLoader(customGroupSize);
            return LoadGenericKernel<TDelegate, GroupedKernelLoader>(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                new KernelSpecialization(customGroupSize, null),
                ref loader);
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
            where TDelegate : Delegate
        {
            var loader = AutoKernelLoader.Default;
            var result = LoadGenericKernel<TDelegate, AutoKernelLoader>(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                KernelSpecialization.Empty,
                ref loader);
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
            where TDelegate : Delegate
        {
            var loader = AutoKernelLoader.Default;
            return LoadGenericKernel<TDelegate, AutoKernelLoader>(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                KernelSpecialization.Empty,
                ref loader);
        }

        // Delegate loaders

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        public TDelegate LoadKernel<TDelegate, TSourceDelegate>(TSourceDelegate methodDelegate)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadKernel<TDelegate>(methodDelegate.GetMethodInfo());

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        public TDelegate LoadKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate,
            KernelSpecialization specialization)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadKernel<TDelegate>(methodDelegate.GetMethodInfo(), specialization);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implictly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        public TDelegate LoadImplicitlyGroupedKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate,
            int customGroupSize)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadImplicitlyGroupedKernel<TDelegate>(methodDelegate.GetMethodInfo(), customGroupSize);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <param name="groupSize">The estimated group size to gain maximum occupancy on this device.</param>
        /// <param name="minGridSize">The minimum grid size to gain maximum occupancy on this device.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate,
            out int groupSize,
            out int minGridSize)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadAutoGroupedKernel<TDelegate>(
                methodDelegate.GetMethodInfo(),
                out groupSize,
                out minGridSize);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedKernel<TDelegate, TSourceDelegate>(TSourceDelegate methodDelegate)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadAutoGroupedKernel<TDelegate>(methodDelegate.GetMethodInfo());

        #endregion
    }
}
