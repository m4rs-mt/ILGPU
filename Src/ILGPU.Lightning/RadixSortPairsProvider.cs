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
using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a platform and accelerator-specific implementation of a radix-sort-pairs operation.
    /// </summary>
    internal abstract partial class RadixSortPairsProviderImplementation : LightningObject
    {
        #region Instance

        protected RadixSortPairsProviderImplementation(Accelerator accelerator)
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
    /// Represents a platform and accelerator-specific implementation of a radix-sort-pairs operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed partial class RadixSortPairsProvider : LightningObject
    {
        #region Instance

        private MemoryBufferCache bufferCache;
        private RadixSortPairsProviderImplementation implementation;

        internal RadixSortPairsProvider(
            Accelerator accelerator,
            RadixSortPairsProviderImplementation implementation)
            : base(accelerator)
        {
            bufferCache = new MemoryBufferCache(Accelerator);
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
    /// Represents a platform and accelerator-specific implementation of a radix-sort-pairs operation.
    /// </summary>
    /// <typeparam name="TValue">The value type of the value elements to be sorted.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed partial class RadixSortPairsProvider<TValue> : LightningObject
        where TValue : struct
    {
        #region Instance

        private MemoryBufferCache bufferCache;
        private MemoryBufferCache sequenceCache;
        private RadixSortPairsProviderImplementation implementation;
        private Sequencer<Index, Sequencers.IndexSequencer> sequencer;

        internal RadixSortPairsProvider(
            Accelerator accelerator,
            RadixSortPairsProviderImplementation implementation)
            : base(accelerator)
        {
            bufferCache = new MemoryBufferCache(Accelerator);
            sequenceCache = new MemoryBufferCache(Accelerator);
            this.implementation = implementation;
            sequencer = accelerator.CreateSequencer<
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

    partial class RadixSortExtensions
    {
        #region RadixSortPairs

        /// <summary>
        /// Extension provider for radix-sort-pairs extensions
        /// </summary>
        partial struct RadixSortPairsExtension : IAcceleratorExtensionProvider<RadixSortPairsProviderImplementation>
        { }

        /// <summary>
        /// Creates a new provider key-value-pair-sorting implementation for radix sort.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created scan provider.</returns>
        internal static RadixSortPairsProviderImplementation CreateRadixSortPairsProviderImplementation(
            this Accelerator accelerator)
        {
            return accelerator.CreateExtension<RadixSortPairsProviderImplementation, RadixSortPairsExtension>(
                new RadixSortPairsExtension());
        }

        /// <summary>
        /// Creates a new specialized key-value-pair-sorting provider for radix sort that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a construction method")]
        public static RadixSortPairsProvider CreateRadixSortPairsProvider(this Accelerator accelerator)
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));
            return new RadixSortPairsProvider(accelerator, accelerator.CreateRadixSortPairsProviderImplementation());
        }

        /// <summary>
        /// Creates a new specialized key-value-pair-sorting provider for radix sort that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <typeparam name="TValue">The value type of the value elements to be sorted.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a construction method")]
        public static RadixSortPairsProvider<TValue> CreateRadixSortPairsProvider<TValue>(this Accelerator accelerator)
            where TValue : struct
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));
            return new RadixSortPairsProvider<TValue>(accelerator, accelerator.CreateRadixSortPairsProviderImplementation());
        }

        #endregion
    }
}
