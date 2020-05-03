// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SpecializationCache.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Runtime
{
    /// <summary>
    /// The base interface for all automatically generated specialization argument
    /// structures that are used in combination with the
    /// <see cref="SpecializationCache{TLoader, TArgs, TDelegate}"/>.
    /// </summary>
    internal interface ISpecializationCacheArgs
    {
        /// <summary>
        /// Returns the i-th argument as an untyped managed object.
        /// </summary>
        /// <param name="index">The argument index.</param>
        /// <returns>The resolved untyped managed object.</returns>
        object GetSpecializedArg(int index);
    }

    /// <summary>
    /// A specialization cache to store and managed specialized kernel versions.
    /// </summary>
    /// <typeparam name="TLoader">The associated loader type.</typeparam>
    /// <typeparam name="TArgs">The arguments key type for caching.</typeparam>
    /// <typeparam name="TDelegate">The launcher delegate type.</typeparam>
    internal class SpecializationCache<TLoader, TArgs, TDelegate> : DisposeBase
        where TLoader : struct, Accelerator.IKernelLoader
        where TArgs : struct, ISpecializationCacheArgs
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
        /// Creates a kernel wrapper method that invokes the actual kernel method
        /// with specialized values.
        /// </summary>
        /// <param name="kernelMethod">The kernel method to invoke.</param>
        /// <param name="args">The target arguments.</param>
        /// <returns>The created IR method.</returns>
        private Method CreateKernelWrapper(Method kernelMethod, in TArgs args)
        {
            // Create a new wrapper kernel launcher
            var handle = MethodHandle.Create("LauncherWrapper");
            var targetContext = kernelMethod.Context;
            var targetMethod = targetContext.Declare(
                new MethodDeclaration(
                    handle,
                    targetContext.VoidType),
                out var _);
            kernelMethod.AddFlags(MethodFlags.Inline);
            using (var methodBuilder = targetMethod.CreateBuilder())
            {
                var location = targetMethod.Location;
                var blockBuilder = methodBuilder.CreateEntryBlock();

                // Append all parameters
                var targetValues = ImmutableArray.CreateBuilder<ValueReference>(
                    kernelMethod.NumParameters);
                var specializedParameters = Entry.Parameters.SpecializedParameters;
                int paramOffset = Entry.KernelIndexParameterOffset;
                for (
                    int i = 0, specialParamIdx = 0, e = kernelMethod.NumParameters;
                    i < e;
                    ++i)
                {
                    var param = kernelMethod.Parameters[i];
                    // Append parameter in all cases to ensure compatibility
                    // with the current argument mapper implementations
                    // TODO: remove these parameters and adapt all argument mappers
                    var newParam = methodBuilder.AddParameter(param.Type, param.Name);

                    // Check for a specialized parameter
                    if (specialParamIdx < specializedParameters.Length &&
                        specializedParameters[specialParamIdx].Index == i - paramOffset)
                    {
                        // Resolve the managed argument -> note that this object cannot
                        // be null
                        var managedArgument = args.GetSpecializedArg(specialParamIdx);
                        var irValue = blockBuilder.CreateValue(
                            location,
                            managedArgument,
                            managedArgument.GetType());

                        targetValues.Add(irValue);
                        ++specialParamIdx;
                    }
                    else
                    {
                        targetValues.Add(newParam);
                    }
                }
                blockBuilder.CreateCall(
                    location,
                    kernelMethod,
                    targetValues.MoveToImmutable());
                blockBuilder.CreateReturn(location);
            }
            return targetMethod;
        }

        /// <summary>
        /// Specializes a kernel with the given customized arguments.
        /// </summary>
        /// <param name="args">The argument structure.</param>
        /// <returns>The specialized kernel launcher.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TDelegate SpecializeKernel(in TArgs args)
        {
            using var targetContext = new IRContext(KernelContext.Context);
            var oldKernelMethod = targetContext.Import(
                KernelMethod,
                new NewScopeProvider());
            var targetMethod = CreateKernelWrapper(oldKernelMethod, args);
            targetContext.Optimize();

            var compiledKernel = Accelerator.Backend.Compile(
                targetMethod,
                Entry,
                KernelSpecialization);
            var kernel = Loader.LoadKernel(Accelerator, compiledKernel);
            return kernel.CreateLauncherDelegate<TDelegate>();
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
