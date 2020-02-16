// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: SpecializationCache.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime
{
    /// <summary>
    /// A specialization cache to store and managed specialized kernel versions.
    /// </summary>
    /// <typeparam name="TLoader">The associated loader type.</typeparam>
    /// <typeparam name="TArgs">The arguments key type for caching.</typeparam>
    /// <typeparam name="TDelegate">The launcher delegate type.</typeparam>
    internal class SpecializationCache<TLoader, TArgs, TDelegate> : DisposeBase
        where TLoader : struct, Accelerator.IKernelLoader
        where TArgs : struct
        where TDelegate : Delegate
    {
        #region Instance

        private readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
        private readonly Dictionary<TArgs, TDelegate> kernelCache =
            new Dictionary<TArgs, TDelegate>();

        /// <summary>
        /// Constructs a new specialization cache.
        /// </summary>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="kernelMethod">The IR kernel method.</param>
        /// <param name="loader">The loader instance.</param>
        /// <param name="entry">The associated entry point.</param>
        /// <param name="specialization">The kernel specialization.</param>
        public SpecializationCache(
            Accelerator accelerator,
            Method kernelMethod,
            TLoader loader,
            EntryPointDescription entry,
            KernelSpecialization specialization)
        {
            Accelerator = accelerator;
            KernelMethod = kernelMethod;
            Loader = loader;
            Entry = entry;
            KernelSpecialization = specialization;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }

        /// <summary>
        /// Returns the associated raw kernel context.
        /// </summary>
        public IRContext KernelContext => KernelMethod.Context;

        /// <summary>
        /// Returns the associated raw kernel method.
        /// </summary>
        public Method KernelMethod { get; }

        /// <summary>
        /// Returns the associated kernel loader.
        /// </summary>
        public TLoader Loader { get; }

        /// <summary>
        /// Returns the current entry point description.
        /// </summary>
        public EntryPointDescription Entry { get; }

        /// <summary>
        /// Returns the current kernel specialization.
        /// </summary>
        public KernelSpecialization KernelSpecialization { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Specializes a kernel with the given customized arguments.
        /// </summary>
        /// <param name="args">The argument structure.</param>
        /// <returns>The specialized kernel launcher.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TDelegate SpecializeKernel(in TArgs args)
        {
            using (var targetContext = new IRContext(KernelContext.Context))
            {
                var targetMethod = targetContext.Import(
                    KernelMethod,
                    new NewScopeProvider());
                targetContext.Optimize();

                var compiledKernel = Accelerator.Backend.Compile(
                    targetMethod,
                    Entry,
                    KernelSpecialization);
                var kernel = Loader.LoadKernel(Accelerator, compiledKernel);
                return kernel.CreateLauncherDelegate<TDelegate>();
            }
        }

        /// <summary>
        /// Gets or creates a specialized kernel based on the arguments provided.
        /// </summary>
        /// <param name="args">The arguments used to specialize the kernel.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TDelegate GetOrCreateKernel(TArgs args)
        {
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                if (!kernelCache.TryGetValue(args, out var launcher))
                {
                    cacheLock.EnterWriteLock();
                    try
                    {
                        launcher = SpecializeKernel(args);
                        kernelCache.Add(args, launcher);
                    }
                    finally
                    {
                        cacheLock.ExitWriteLock();
                    }
                }
                return launcher;
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
            }
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                KernelContext.Dispose();
                cacheLock.Dispose();
                kernelCache.Clear();
            }
        }

        #endregion
    }
}
