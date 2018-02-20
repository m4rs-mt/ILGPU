// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: RadixSortProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a platform and accelerator-specific implementation of a radix-sort operation.
    /// </summary>
    internal abstract partial class RadixSortProviderImplementation : LightningObject
    {
        #region Instance

        protected RadixSortProviderImplementation(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        { }

        #endregion
    }

    /// <summary>
    /// Represents a radix-sort provider for a radix-sort operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed partial class RadixSortProvider : LightningObject
    {
        #region Instance

        private MemoryBufferCache bufferCache;
        private RadixSortProviderImplementation implementation;

        internal RadixSortProvider(Accelerator accelerator, RadixSortProviderImplementation implementation)
            : base(accelerator)
        {
            bufferCache = new MemoryBufferCache(accelerator);
            this.implementation = implementation;
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "bufferCache", Justification = "Dispose method will be invoked by a helper method")]
        protected override void Dispose(bool disposing)
        {
            Dispose(ref bufferCache);
            Dispose(ref implementation);
        }

        #endregion
    }

    /// <summary>
    /// Radix-sort functionality for accelerators.
    /// </summary>
    public static partial class RadixSortExtensions
    {
        #region RadixSort

        /// <summary>
        /// Extension provider for radix-sort extensions
        /// </summary>
        partial struct RadixSortExtension : IAcceleratorExtensionProvider<RadixSortProviderImplementation>
        { }

        /// <summary>
        /// Creates a new provider implementation for radix sort.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        internal static RadixSortProviderImplementation CreateRadixSortProviderImplementation(this Accelerator accelerator)
        {
            return accelerator.CreateExtension<RadixSortProviderImplementation, RadixSortExtension>(
                new RadixSortExtension());
        }

        /// <summary>
        /// Creates a new specialized radix-sort provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a construction method")]
        public static RadixSortProvider CreateRadixSortProvider(this Accelerator accelerator)
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));
            return new RadixSortProvider(accelerator, accelerator.CreateRadixSortProviderImplementation());
        }

        #endregion
    }
}
