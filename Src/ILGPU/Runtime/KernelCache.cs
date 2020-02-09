// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelCache.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.EntryPoints;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Constants

        /// <summary>
        /// Constant to control GC invocations.
        /// </summary>
        private const int NumberNewKernelsUntilGC = 128;

        /// <summary>
        /// Minimum number of kernel objects before we apply GC.
        /// </summary>
        /// <remarks>Should be less or equal to <see cref="NumberNewKernelsUntilGC"/></remarks>
        private const int MinNumberOfKernelsInGC = 128;

        #endregion

        #region Nested Types

        /// <summary>
        /// A cached kernel key.
        /// </summary>
        private readonly struct CachedCompiledKernelKey : IEquatable<CachedCompiledKernelKey>
        {
            #region Instance

            /// <summary>
            /// Constructs a new kernel key.
            /// </summary>
            /// <param name="entry">The entry point description.</param>
            /// <param name="specialization">The kernel specialization.</param>
            public CachedCompiledKernelKey(
                EntryPointDescription entry,
                KernelSpecialization specialization)
            {
                Entry = entry;
                Specialization = specialization;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated entry point description.
            /// </summary>
            public EntryPointDescription Entry { get; }

            /// <summary>
            /// Returns the associated kernel specialization.
            /// </summary>
            public KernelSpecialization Specialization { get; }

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true if the given cached key is equal to the current one.
            /// </summary>
            /// <param name="other">The other key.</param>
            /// <returns>True, if the given cached key is equal to the current one.</returns>
            public bool Equals(CachedCompiledKernelKey other) =>
                other.Entry == Entry &&
                other.Specialization.Equals(Specialization);

            #endregion

            #region Object

            /// <summary>
            /// Returns true if the given object is equal to the current one.
            /// </summary>
            /// <param name="obj">The other object.</param>
            /// <returns>True, if the given object is equal to the current one.</returns>
            public override bool Equals(object obj) =>
                obj is CachedCompiledKernelKey other && Equals(other);

            /// <summary>
            /// Returns the hash code of this object.
            /// </summary>
            /// <returns>The hash code of this object.</returns>
            public override int GetHashCode() =>
                Entry.GetHashCode() ^ Specialization.GetHashCode();

            /// <summary>
            /// Returns the string representation of this object.
            /// </summary>
            /// <returns>The string representation of this object.</returns>
            public override string ToString() =>
                $"{Entry} [Specialization: {Specialization}]";

            #endregion
        }

        /// <summary>
        /// A cached kernel key.
        /// </summary>
        private readonly struct CachedKernelKey : IEquatable<CachedKernelKey>
        {
            #region Instance

            /// <summary>
            /// Constructs a new kernel key.
            /// </summary>
            /// <param name="compiledKernelKey">The compiled kernel key for lookup purposes.</param>
            /// <param name="implicitGroupSize">The implicit group size (if any).</param>
            public CachedKernelKey(CachedCompiledKernelKey compiledKernelKey, int implicitGroupSize)
            {
                CompiledKernelKey = compiledKernelKey;
                ImplicitGroupSize = implicitGroupSize;
            }

            #endregion

            #region Properties

            /// <summary>
            /// The associated compiled kernel key for lookup purposes.
            /// </summary>
            public CachedCompiledKernelKey CompiledKernelKey { get; }

            /// <summary>
            /// Returns the associated implicit group size.
            /// </summary>
            public int ImplicitGroupSize { get; }

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true if the given cached key is equal to the current one.
            /// </summary>
            /// <param name="other">The other key.</param>
            /// <returns>True, if the given cached key is equal to the current one.</returns>
            public bool Equals(CachedKernelKey other) =>
                other.CompiledKernelKey.Equals(CompiledKernelKey) &&
                other.ImplicitGroupSize == ImplicitGroupSize;

            #endregion

            #region Object

            /// <summary>
            /// Returns true if the given object is equal to the current one.
            /// </summary>
            /// <param name="obj">The other object.</param>
            /// <returns>True, if the given object is equal to the current one.</returns>
            public override bool Equals(object obj) =>
                obj is CachedKernelKey other && Equals(other);

            /// <summary>
            /// Returns the hash code of this object.
            /// </summary>
            /// <returns>The hash code of this object.</returns>
            public override int GetHashCode() =>
                CompiledKernelKey.GetHashCode() ^ ImplicitGroupSize;

            /// <summary>
            /// Returns the string representation of this object.
            /// </summary>
            /// <returns>The string representation of this object.</returns>
            public override string ToString() =>
                $"{CompiledKernelKey} [GroupSize: {ImplicitGroupSize}]";

            #endregion
        }

        /// <summary>
        /// A cached kernel.
        /// </summary>
        private struct CachedKernel
        {
            #region Instance

            private WeakReference<Kernel> kernelReference;

            /// <summary>
            /// Constructs a new cached kernel.
            /// </summary>
            /// <param name="kernel">The kernel to cache.</param>
            /// <param name="groupSize">The computed group size.</param>
            /// <param name="minGridSize">The computed minimum grid size.</param>
            public CachedKernel(
                WeakReference<Kernel> kernel,
                int groupSize,
                int minGridSize)
            {
                kernelReference = kernel;
                GroupSize = groupSize;
                MinGridSize = minGridSize;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the computed group size.
            /// </summary>
            public int GroupSize { get; }

            /// <summary>
            /// Returns the computed minimum grid size.
            /// </summary>
            public int MinGridSize { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Tries to resolve the associated kernel.
            /// </summary>
            /// <param name="kernel">The resolved kernel.</param>
            /// <returns>True, iff the associated kernel could be resolved.</returns>
            public bool TryGetKernel(out Kernel kernel)
            {
                return kernelReference.TryGetTarget(out kernel);
            }

            /// <summary>
            /// Tries to update the internal weak reference or creates a new one
            /// pointing to the given target.
            /// </summary>
            /// <param name="target">The new target kernel.</param>
            /// <returns>An updated weak reference that points to the given target.</returns>
            public WeakReference<Kernel> UpdateReference(Kernel target)
            {
                if (kernelReference != null)
                    kernelReference.SetTarget(target);
                else
                    kernelReference = new WeakReference<Kernel>(target);
                return kernelReference;
            }

            #endregion
        }

        /// <summary>
        /// Represents a generic kernel loader.
        /// </summary>
        private interface IKernelLoader
        {
            /// <summary>
            /// Returns the custom group size.
            /// </summary>
            int GroupSize { get; set; }

            /// <summary>
            /// Returns the custom min grid size.
            /// </summary>
            int MinGridSize { get; set; }

            /// <summary>
            /// Loads the given kernel using the given accelerator.
            /// </summary>
            /// <param name="accelerator">The target accelerator for the loading operation.</param>
            /// <param name="compiledKernel">The compiled kernel to load.</param>
            /// <returns>The loaded kernel.</returns>
            Kernel LoadKernel(Accelerator accelerator, CompiledKernel compiledKernel);
        }

        #endregion

        #region Instance

        /// <summary>
        /// A cache for compiled kernel objects.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Dictionary<CachedCompiledKernelKey, WeakReference<CompiledKernel>> compiledKernelCache;

        /// <summary>
        /// A cache for loaded kernel objects.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Dictionary<CachedKernelKey, CachedKernel> kernelCache;

        /// <summary>
        /// Initializes the local kernel cache.
        /// </summary>
        private void InitKernelCache()
        {
            if (Context.HasFlags(ContextFlags.DisableKernelCaching))
                return;

            compiledKernelCache = new Dictionary<CachedCompiledKernelKey, WeakReference<CompiledKernel>>();
            kernelCache = new Dictionary<CachedKernelKey, CachedKernel>();
        }

        #endregion

        #region Internal Properties

        /// <summary>
        /// Returns true if the kernel cache is enabled.
        /// </summary>
        private bool KernelCacheEnabled => kernelCache != null;

        /// <summary>
        /// True, iff a GC run is requested to clean disposed child kernels.
        /// </summary>
        /// <remarks>This method is invoked in the scope of the locked <see cref="syncRoot"/> object.</remarks>
        private bool RequestKernelCacheGC_SyncRoot =>
            KernelCacheEnabled &&
            ((compiledKernelCache.Count % NumberNewKernelsUntilGC) == 0 ||
            (kernelCache.Count % NumberNewKernelsUntilGC) == 0);

        #endregion

        #region Methods

        /// <summary>
        /// Loads a kernel specified by the given method without using internal caches.
        /// </summary>
        /// <typeparam name="TKernelLoader">The type of the custom kernel loader.</typeparam>
        /// <param name="entry">The entry point to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <returns>The loaded kernel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Kernel LoadGenericKernelDirect<TKernelLoader>(
            EntryPointDescription entry,
            KernelSpecialization specialization,
            ref TKernelLoader kernelLoader)
            where TKernelLoader : struct, IKernelLoader
        {
            var compiledKernel = CompileKernel(entry, specialization);
            return kernelLoader.LoadKernel(this, compiledKernel);
        }

        /// <summary>
        /// Loads a kernel specified by the given method.
        /// </summary>
        /// <typeparam name="TKernelLoader">The type of the custom kernel loader.</typeparam>
        /// <param name="entry">The entry point to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="kernelLoader">The kernel loader.</param>
        /// <returns>The loaded kernel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Kernel LoadGenericKernel<TKernelLoader>(
            EntryPointDescription entry,
            KernelSpecialization specialization,
            ref TKernelLoader kernelLoader)
            where TKernelLoader : struct, IKernelLoader
        {
            if (KernelCacheEnabled)
            {
                var cachedCompiledKernelKey = new CachedCompiledKernelKey(entry, specialization);
                var cachedKey = new CachedKernelKey(cachedCompiledKernelKey, kernelLoader.GroupSize);
                lock (syncRoot)
                {
                    if (!kernelCache.TryGetValue(cachedKey, out CachedKernel cached) ||
                        !cached.TryGetKernel(out Kernel result))
                    {
                        result = LoadGenericKernelDirect(entry, specialization, ref kernelLoader);
                        kernelCache[cachedKey] = new CachedKernel(
                            cached.UpdateReference(result),
                            kernelLoader.GroupSize,
                            kernelLoader.MinGridSize);
                    }
                    else
                    {
                        kernelLoader.MinGridSize = cached.MinGridSize;
                        kernelLoader.GroupSize = cached.GroupSize;
                    }
                    RequestGC_SyncRoot();
                    return result;
                }
            }
            else
                return LoadGenericKernelDirect(entry, specialization, ref kernelLoader);
        }

        /// <summary>
        /// Compiles the given method into a <see cref="CompiledKernel"/>.
        /// </summary>
        /// <param name="entry">The entry point to compile into a <see cref="CompiledKernel"/>.</param>
        /// <returns>The compiled kernel.</returns>
        public CompiledKernel CompileKernel(EntryPointDescription entry) =>
            CompileKernel(entry, KernelSpecialization.Empty);

        /// <summary>
        /// Compiles the given method into a <see cref="CompiledKernel"/> using the given
        /// kernel specialization.
        /// </summary>
        /// <param name="entry">The entry point to compile into a <see cref="CompiledKernel"/>.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <returns>The compiled kernel.</returns>
        public CompiledKernel CompileKernel(EntryPointDescription entry, KernelSpecialization specialization)
        {
            // Check for compatiblity
            if (!specialization.IsCompatibleWith(this))
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedKernelSpecialization);

            if (KernelCacheEnabled)
            {
                // Check and update cache
                var cachedKey = new CachedCompiledKernelKey(entry, specialization);
                lock (syncRoot)
                {
                    if (!compiledKernelCache.TryGetValue(cachedKey, out WeakReference<CompiledKernel> cached) ||
                        !cached.TryGetTarget(out CompiledKernel result))
                    {
                        result = Backend.Compile(entry, specialization);
                        if (cached == null)
                            compiledKernelCache.Add(cachedKey, new WeakReference<CompiledKernel>(result));
                        else
                            cached.SetTarget(result);
                    }
                    RequestGC_SyncRoot();
                    return result;
                }
            }
            else
                return Backend.Compile(entry, specialization);
        }

        /// <summary>
        /// Clears the internal cache cache.
        /// </summary>
        private void ClearKernelCache_SyncRoot()
        {
            if (!KernelCacheEnabled)
                return;
            compiledKernelCache.Clear();
            kernelCache.Clear();
        }

        /// <summary>
        /// GC method to clean disposed kernels.
        /// </summary>
        /// <remarks>This method is invoked in the scope of the locked <see cref="syncRoot"/> object.</remarks>
        private void KernelCacheGC_SyncRoot()
        {
            if (!KernelCacheEnabled)
                return;

            if (compiledKernelCache.Count >= MinNumberOfKernelsInGC)
            {
                var oldCompiledKernels = compiledKernelCache;
                compiledKernelCache = new Dictionary<CachedCompiledKernelKey, WeakReference<CompiledKernel>>();
                foreach (var entry in oldCompiledKernels)
                {
                    if (entry.Value.TryGetTarget(out CompiledKernel _))
                        compiledKernelCache.Add(entry.Key, entry.Value);
                }
            }

            if (kernelCache.Count >= MinNumberOfKernelsInGC)
            {
                var oldKernels = kernelCache;
                kernelCache = new Dictionary<CachedKernelKey, CachedKernel>();
                foreach (var entry in oldKernels)
                {
                    if (entry.Value.TryGetKernel(out Kernel _))
                        kernelCache.Add(entry.Key, entry.Value);
                }
            }
        }

        #endregion
    }
}
