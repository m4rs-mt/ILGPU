// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: RadixSortPairsProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a platform and accelerator-specific implementation of a radix-sort-pairs operation.
    /// </summary>
    internal abstract partial class RadixSortPairsProviderImplementation : LightningContextObject
    {
        #region Instance

        protected RadixSortPairsProviderImplementation(LightningContext lightningContext)
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
    /// Represents a platform and accelerator-specific implementation of a radix-sort-pairs operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed partial class RadixSortPairsProvider : LightningContextObject
    {
        #region Instance

        private MemoryBufferCache bufferCache;
        private RadixSortPairsProviderImplementation implementation;

        internal RadixSortPairsProvider(LightningContext lightningContext, RadixSortPairsProviderImplementation implementation)
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

    /// <summary>
    /// Represents a platform and accelerator-specific implementation of a radix-sort-pairs operation.
    /// </summary>
    /// <typeparam name="TValue">The value type of the value elements to be sorted.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed partial class RadixSortPairsProvider<TValue> : LightningContextObject
        where TValue : struct
    {
        #region Instance

        private MemoryBufferCache bufferCache;
        private MemoryBufferCache sequenceCache;
        private RadixSortPairsProviderImplementation implementation;
        private Sequencer<Index, Sequencers.IndexSequencer> sequencer;

        internal RadixSortPairsProvider(
            LightningContext lightningContext,
            RadixSortPairsProviderImplementation implementation)
            : base(lightningContext)
        {
            bufferCache = new MemoryBufferCache(Accelerator);
            sequenceCache = new MemoryBufferCache(Accelerator);
            this.implementation = implementation;
            sequencer = lightningContext.CreateSequencer<
                Index, Sequencers.IndexSequencer>();
        }

        #endregion

        #region Methods

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            Dispose(ref bufferCache);
            Dispose(ref sequenceCache);
            Dispose(ref implementation);
        }

        #endregion
    }

    partial class LightningContext
    {
        #region Instance

        /// <summary>
        /// Internal radix-sort-pairs implementation.
        /// </summary>
        private RadixSortPairsProviderImplementation radixSortPairsImplementation;

        /// <summary>
        /// </summary>
        private void InitRadixSortPairs()
        {
            radixSortPairsImplementation = CreateRadixSortPairsProviderImplementation();
        }

        /// <summary>
        /// Disposes the radix-sort-pairs implementation.
        /// </summary>
        private void DisposeRadixSortPairs()
        {
            Dispose(ref radixSortPairsImplementation);
        }

        #endregion

        #region RadixSort

        /// <summary>
        /// Extension provider for radix-sort-pairs extensions
        /// </summary>
        partial struct RadixSortPairsExtension : IAcceleratorExtensionProvider<RadixSortPairsProviderImplementation>
        {
            public RadixSortPairsExtension(LightningContext lightningContext)
            {
                LightningContext = lightningContext;
            }

            /// <summary>
            /// Returns the current lightning context.
            /// </summary>
            public LightningContext LightningContext { get; }
        }

        /// <summary>
        /// Creates a new provider key-value-pair-sorting implementation for radix sort.
        /// </summary>
        /// <returns>The created scan provider.</returns>
        private RadixSortPairsProviderImplementation CreateRadixSortPairsProviderImplementation()
        {
            return Accelerator.CreateExtension<RadixSortPairsProviderImplementation, RadixSortPairsExtension>(
                new RadixSortPairsExtension(this));
        }

        /// <summary>
        /// Creates a new specialized key-value-pair-sorting provider for radix sort that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <returns>The created provider.</returns>
        public RadixSortPairsProvider CreateRadixSortPairsProvider()
        {
            return new RadixSortPairsProvider(this, CreateRadixSortPairsProviderImplementation());
        }

        /// <summary>
        /// Creates a new specialized key-value-pair-sorting provider for radix sort that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <typeparam name="TValue">The value type of the value elements to be sorted.</typeparam>
        /// <returns>The created provider.</returns>
        public RadixSortPairsProvider<TValue> CreateRadixSortPairsProvider<TValue>()
            where TValue : struct
        {
            return new RadixSortPairsProvider<TValue>(this, CreateRadixSortPairsProviderImplementation());
        }

        #endregion
    }
}
