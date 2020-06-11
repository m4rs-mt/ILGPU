// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: KernelLoading.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
        /// Adjusts and verifies a custom group size of a specific kernel.
        /// Note that this function ensures that implicitly grouped kernels
        /// without an explicit group size will be launched with a group size
        /// that is equal to the available warp size.
        /// </summary>
        /// <param name="customGroupSize">
        /// The custom group size to adjust and verify.
        /// </param>
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
                {
                    throw new InvalidOperationException(
                        RuntimeErrorMessages.InvalidCustomGroupSize);
                }
            }
            else if (customGroupSize == 0)
            {
                customGroupSize = WarpSize;
            }

            var maxNumThreadsPerGroup = entryPoint.Specialization.MaxNumThreadsPerGroup;
            if (maxNumThreadsPerGroup.HasValue &&
                customGroupSize > maxNumThreadsPerGroup.Value)
            {
                throw new InvalidOperationException(
                    RuntimeErrorMessages.InvalidKernelSpecializationGroupSize);
            }
        }

        #endregion

        #region Direct Kernel Loading

        /// <summary>
        /// Loads the given explicitly grouped kernel.
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
        /// Loads the given explicitly grouped kernel.
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
        /// Note that implicitly-grouped kernel will be launched with the given group
        /// size.
        /// </remarks>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// </remarks>
        public Kernel LoadImplicitlyGroupedKernel(
            CompiledKernel kernel,
            int customGroupSize)
        {
            Bind();
            return LoadImplicitlyGroupedKernelInternal(
                kernel,
                customGroupSize,
                out var _);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <param name="kernelInfo">
        /// Detailed kernel information about the loaded kernel.
        /// </param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that implicitly-grouped kernel will be launched with the given group
        /// size.
        /// </remarks>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// </remarks>
        public Kernel LoadImplicitlyGroupedKernel(
            CompiledKernel kernel,
            int customGroupSize,
            out KernelInfo kernelInfo)
        {
            Bind();
            return LoadImplicitlyGroupedKernelInternal(
                kernel,
                customGroupSize,
                out kernelInfo);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <param name="kernelInfo">
        /// Detailed kernel information about the loaded kernel.
        /// </param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that implicitly-grouped kernel will be launched with the given group
        /// size.
        /// </remarks>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// </remarks>
        protected abstract Kernel LoadImplicitlyGroupedKernelInternal(
            CompiledKernel kernel,
            int customGroupSize,
            out KernelInfo kernelInfo);

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
            LoadAutoGroupedKernel(kernel, out var _);

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="kernelInfo">
        /// Detailed kernel information about the loaded kernel.
        /// </param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// </remarks>
        public Kernel LoadAutoGroupedKernel(
            CompiledKernel kernel,
            out KernelInfo kernelInfo)
        {
            Bind();
            return LoadAutoGroupedKernelInternal(kernel, out kernelInfo);
        }

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="kernel">The kernel to load.</param>
        /// <param name="kernelInfo">
        /// Detailed kernel information about the loaded kernel.
        /// </param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel will not be managed by the kernel cache.
        /// </remarks>
        protected abstract Kernel LoadAutoGroupedKernelInternal(
            CompiledKernel kernel,
            out KernelInfo kernelInfo);

        #endregion

        #region Generic Kernel Loading

        /// <summary>
        /// Represents a default kernel loader.
        /// </summary>
        private struct DefaultKernelLoader : IKernelLoader
        {
            /// <summary>
            /// Returns 0.
            /// </summary>
            public int GroupSize => 0;

            /// <summary>
            /// Loads an explicitly grouped kernel.
            /// </summary>
            public Kernel LoadKernel(
                Accelerator accelerator,
                CompiledKernel compiledKernel,
                out KernelInfo kernelInfo)
            {
                var kernel = accelerator.LoadKernel(compiledKernel);
                kernelInfo = KernelInfo.CreateFrom(
                    compiledKernel.Info,
                    null,
                    null);
                return kernel;
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
            }

            /// <summary>
            /// Returns the assigned group size.
            /// </summary>
            public int GroupSize { get; }

            /// <summary>
            /// Loads an implicitly grouped kernel.
            /// </summary>
            public Kernel LoadKernel(
                Accelerator accelerator,
                CompiledKernel compiledKernel,
                out KernelInfo kernelInfo) =>
                accelerator.LoadImplicitlyGroupedKernel(
                    compiledKernel,
                    GroupSize,
                    out kernelInfo);
        }

        /// <summary>
        /// Represents an automatically configured grouped kernel loader for
        /// implicitly-grouped kernels.
        /// </summary>
        private struct AutoKernelLoader : IKernelLoader
        {
            /// <summary cref="IKernelLoader.GroupSize"/>
            public int GroupSize => -1;

            /// <summary>
            /// Loads an automatically grouped kernel.
            /// </summary>
            public Kernel LoadKernel(
                Accelerator accelerator,
                CompiledKernel compiledKernel,
                out KernelInfo kernelInfo) =>
                accelerator.LoadAutoGroupedKernel(
                    compiledKernel,
                    out kernelInfo);
        }

        /// <summary>
        /// Loads a kernel specified by the given method and returns a launcher of the
        /// specified type. Note that implicitly-grouped kernels will be launched with
        /// a group size of the current warp size of the accelerator.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <typeparam name="TKernelLoader">
        /// The type of the custom kernel loader.
        /// </typeparam>
        /// <param name="entry">The entry point to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TDelegate LoadGenericKernel<TDelegate, TKernelLoader>(
            in EntryPointDescription entry,
            in KernelSpecialization specialization,
            in TKernelLoader kernelLoader,
            out KernelInfo kernelInfo)
            where TDelegate : Delegate
            where TKernelLoader : struct, IKernelLoader
        {
            // Check for specialized parameters
            if (entry.HasSpecializedParameters)
            {
                return LoadSpecializationKernel<TDelegate, TKernelLoader>(
                    entry,
                    specialization,
                    kernelLoader,
                    out kernelInfo);
            }

            var kernel = LoadGenericKernel(
                entry,
                specialization,
                kernelLoader,
                out kernelInfo);
            return kernel.CreateLauncherDelegate<TDelegate>();
        }

        #endregion

        #region Kernel Loading

        /// <summary>
        /// Loads the given explicitly grouped kernel. Implicitly-grouped kernels are
        /// not supported.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel must not be disposed manually.
        /// </remarks>
        public Kernel LoadKernel(MethodInfo method) =>
            LoadKernel(method, KernelSpecialization.Empty);

        /// <summary>
        /// Loads the given explicitly grouped kernel. Implicitly-grouped kernels are
        /// not supported.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel must not be disposed manually.
        /// </remarks>
        public Kernel LoadKernel(
            MethodInfo method,
            KernelSpecialization specialization) =>
            LoadKernel(method, specialization, out var _);

        /// <summary>
        /// Loads the given explicitly grouped kernel. Implicitly-grouped kernels are
        /// not supported.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel must not be disposed manually.
        /// </remarks>
        public Kernel LoadKernel(
            MethodInfo method,
            KernelSpecialization specialization,
            out KernelInfo kernelInfo) =>
            LoadGenericKernel(
                EntryPointDescription.FromExplicitlyGroupedKernel(method),
                specialization,
                new DefaultKernelLoader(),
                out kernelInfo);

        /// <summary>
        /// Loads the given implicitly-grouped kernel. Implicitly-grouped kernel
        /// will be launched with the given group size.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel must not be disposed manually.
        /// </remarks>
        public Kernel LoadImplicitlyGroupedKernel(
            MethodInfo method,
            int customGroupSize) =>
            LoadGenericKernel(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                new KernelSpecialization(customGroupSize, null),
                new GroupedKernelLoader(customGroupSize),
                out var _);

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel must not be disposed manually.
        /// </remarks>
        public Kernel LoadAutoGroupedKernel(MethodInfo method) =>
            LoadAutoGroupedKernel(method, out var _);

        /// <summary>
        /// Loads the given implicitly-grouped kernel while using an automatically
        /// computed grouping configuration.
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel.</returns>
        /// <remarks>
        /// Note that the returned kernel must not be disposed manually.
        /// </remarks>
        public Kernel LoadAutoGroupedKernel(
            MethodInfo method,
            out KernelInfo kernelInfo) =>
            LoadGenericKernel(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                KernelSpecialization.Empty,
                new AutoKernelLoader(),
                out kernelInfo);

        /// <summary>
        /// Loads the given explicitly grouped kernel and returns a launcher delegate
        /// that can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implicitly-grouped kernels are not supported.
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
        /// Note that implicitly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        public TDelegate LoadKernel<TDelegate>(
            MethodInfo method,
            KernelSpecialization specialization)
            where TDelegate : Delegate =>
            LoadKernel<TDelegate>(method, specialization, out var _);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implicitly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        public TDelegate LoadKernel<TDelegate>(
            MethodInfo method,
            KernelSpecialization specialization,
            out KernelInfo kernelInfo)
            where TDelegate : Delegate =>
            LoadGenericKernel<TDelegate, DefaultKernelLoader>(
                EntryPointDescription.FromExplicitlyGroupedKernel(method),
                specialization,
                new DefaultKernelLoader(),
                out kernelInfo);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implicitly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        public TDelegate LoadImplicitlyGroupedKernel<TDelegate>(
            MethodInfo method,
            int customGroupSize)
            where TDelegate : Delegate =>
            LoadImplicitlyGroupedKernel<TDelegate>(
                method,
                customGroupSize,
                out var _);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// Note that implicitly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        public TDelegate LoadImplicitlyGroupedKernel<TDelegate>(
            MethodInfo method,
            int customGroupSize,
            out KernelInfo kernelInfo)
            where TDelegate : Delegate =>
            LoadGenericKernel<TDelegate, GroupedKernelLoader>(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                new KernelSpecialization(customGroupSize, null),
                new GroupedKernelLoader(customGroupSize),
                out kernelInfo);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedKernel<TDelegate>(MethodInfo method)
            where TDelegate : Delegate =>
            LoadAutoGroupedKernel<TDelegate>(method, out var _);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <param name="method">The method to compile into a kernel.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedKernel<TDelegate>(
            MethodInfo method,
            out KernelInfo kernelInfo)
            where TDelegate : Delegate =>
            LoadGenericKernel<TDelegate, AutoKernelLoader>(
                EntryPointDescription.FromImplicitlyGroupedKernel(method),
                KernelSpecialization.Empty,
                new AutoKernelLoader(),
                out kernelInfo);

        // Delegate loaders

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implicitly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        public TDelegate LoadKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate,
            out KernelInfo kernelInfo)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadKernel<TDelegate>(
                methodDelegate.GetMethodInfo(),
                KernelSpecialization.Empty,
                out kernelInfo);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implicitly-grouped kernels will be launched with a group size
        /// of the current warp size of the accelerator.
        /// </remarks>
        public TDelegate LoadKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate,
            KernelSpecialization specialization,
            out KernelInfo kernelInfo)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadKernel<TDelegate>(
                methodDelegate.GetMethodInfo(),
                specialization,
                out kernelInfo);

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
        /// Note that implicitly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        public TDelegate LoadImplicitlyGroupedKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate,
            int customGroupSize)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadImplicitlyGroupedKernel<TDelegate>(
                methodDelegate.GetMethodInfo(),
                customGroupSize);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <param name="customGroupSize">The custom group size to use.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        /// <remarks>
        /// Note that implicitly-grouped kernel will be launched with the given
        /// group size.
        /// </remarks>
        public TDelegate LoadImplicitlyGroupedKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate,
            int customGroupSize,
            out KernelInfo kernelInfo)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadImplicitlyGroupedKernel<TDelegate>(
                methodDelegate.GetMethodInfo(),
                customGroupSize,
                out kernelInfo);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <param name="kernelInfo">Detailed kernel information.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate,
            out KernelInfo kernelInfo)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadAutoGroupedKernel<TDelegate>(
                methodDelegate.GetMethodInfo(),
                out kernelInfo);

        /// <summary>
        /// Loads the given kernel and returns a launcher delegate that
        /// can receive arbitrary accelerator streams (first parameter).
        /// </summary>
        /// <typeparam name="TSourceDelegate">The source delegate type.</typeparam>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <param name="methodDelegate">The delegate to compile into a kernel.</param>
        /// <returns>The loaded kernel-launcher delegate.</returns>
        public TDelegate LoadAutoGroupedKernel<TDelegate, TSourceDelegate>(
            TSourceDelegate methodDelegate)
            where TDelegate : Delegate
            where TSourceDelegate : Delegate =>
            LoadAutoGroupedKernel<TDelegate>(methodDelegate.GetMethodInfo());

        #endregion
    }
}

