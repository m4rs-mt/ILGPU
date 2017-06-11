// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: RadixSortProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a platform and accelerator-specific implementation of a radix-sort operation.
    /// </summary>
    internal abstract partial class RadixSortProviderImplementation : LightningContextObject
    {
        #region Instance

        protected RadixSortProviderImplementation(LightningContext lightningContext)
            : base(lightningContext)
        { }

        #endregion

        #region Methods

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
    public sealed partial class RadixSortProvider : LightningContextObject
    {
        #region Instance

        private MemoryBufferCache bufferCache;
        private RadixSortProviderImplementation implementation;

        internal RadixSortProvider(LightningContext lightningContext, RadixSortProviderImplementation implementation)
            : base(lightningContext)
        {
            bufferCache = new MemoryBufferCache(Accelerator);
            this.implementation = implementation;
        }

        #endregion

        #region Methods

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "bufferCache", Justification = "Dispose method will be invoked by a helper method")]
        protected override void Dispose(bool disposing)
        {
            Dispose(ref bufferCache);
            Dispose(ref implementation);
        }

        #endregion
    }

    partial class LightningContext
    {
        #region Instance

        /// <summary>
        /// Internal radix-sort implementation.
        /// </summary>
        private RadixSortProviderImplementation radixSortImplementation;

        /// <summary>
        /// </summary>
        private void InitRadixSort()
        {
            radixSortImplementation = CreateRadixSortProviderImplementation();
        }

        /// <summary>
        /// Disposes the radix-sort implementation.
        /// </summary>
        private void DisposeRadixSort()
        {
            Dispose(ref radixSortImplementation);
        }

        #endregion

        #region RadixSort

        /// <summary>
        /// Extension provider for radix-sort extensions
        /// </summary>
        partial struct RadixSortExtension : IAcceleratorExtensionProvider<RadixSortProviderImplementation>
        {
            public RadixSortExtension(LightningContext lightningContext)
            {
                LightningContext = lightningContext;
            }

            /// <summary>
            /// Returns the current lightning context.
            /// </summary>
            public LightningContext LightningContext { get; }
        }

        /// <summary>
        /// Creates a new provider implementation for radix sort.
        /// </summary>
        /// <returns>The created scan provider.</returns>
        private RadixSortProviderImplementation CreateRadixSortProviderImplementation()
        {
            return Accelerator.CreateExtension<RadixSortProviderImplementation, RadixSortExtension>(
                new RadixSortExtension(this));
        }

        /// <summary>
        /// Creates a new specialized radix-sort provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <returns>The created provider.</returns>
        public RadixSortProvider CreateRadixSortProvider()
        {
            return new RadixSortProvider(this, CreateRadixSortProviderImplementation());
        }

        #endregion
    }
}
