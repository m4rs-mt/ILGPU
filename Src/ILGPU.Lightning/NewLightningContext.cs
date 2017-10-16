// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: NewLightningContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a wrapper object for accelerator instances.
    /// </summary>
    internal sealed partial class NewLightningContext : LightningObject
    {
        #region Constants

        /// <summary>
        /// Represents the name of the native library.
        /// </summary>
        public const string NativeLibName = "ILGPU.Lightning.Native.dll";

        #endregion

        #region Static

        /// <summary>
        /// Represents a cache for lightning contexts.
        /// </summary>
        private static readonly Dictionary<Accelerator, NewLightningContext> cache =
            new Dictionary<Accelerator, NewLightningContext>();

        /// <summary>
        /// Dispose callback that is invoked by every accelerator.
        /// </summary>
        /// <param name="sender">The sender (the accelerator).</param>
        /// <param name="e">The event args (not used)</param>
        private static void DisposedCallback(object sender, EventArgs e)
        {
            var accelerator = sender as Accelerator;
            if (accelerator == null)
                return;
            accelerator.Disposed -= DisposedCallback;
            lock (cache)
            {
                if (!cache.TryGetValue(accelerator, out NewLightningContext lc))
                    return;
                cache.Remove(accelerator);
                lc.Dispose();
            }
        }

        /// <summary>
        /// Resolves the lightning context for the given accelerator.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The associated lightning context.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Object references will be stored in a local cache")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NewLightningContext Get(Accelerator accelerator)
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));

            lock (cache)
            {
                if (!cache.TryGetValue(accelerator, out NewLightningContext result))
                {
                    result = new NewLightningContext(accelerator);
                    accelerator.Disposed += DisposedCallback;
                    cache.Add(accelerator, result);
                }
                return result;
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Internal scan implementation.
        /// </summary>
        private ScanProviderImplementation scanImplementation;

        /// <summary>
        /// Internal radix-sort implementation.
        /// </summary>
        private RadixSortProviderImplementation radixSortImplementation;

        /// <summary>
        /// Internal radix-sort-pairs implementation.
        /// </summary>
        private RadixSortPairsProviderImplementation radixSortPairsImplementation;

        /// <summary>
        /// Constructs a new lightning context.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        private NewLightningContext(Accelerator accelerator)
            : base(accelerator)
        {
            scanImplementation = Accelerator.CreateScanProviderImplementation();
            radixSortImplementation = Accelerator.CreateRadixSortProviderImplementation();
            radixSortPairsImplementation = Accelerator.CreateRadixSortPairsProviderImplementation();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the internal scan implementation.
        /// </summary>
        internal ScanProviderImplementation ScanImplementation => scanImplementation;

        /// <summary>
        /// Returns the internal radix-sort implementation.
        /// </summary>
        internal RadixSortProviderImplementation RadixSortImplementation => radixSortImplementation;

        /// <summary>
        /// Returns the internal radix-sort-pairs implementation.
        /// </summary>
        internal RadixSortPairsProviderImplementation RadixSortPairsImplementation => radixSortPairsImplementation;

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            Dispose(ref scanImplementation);
            Dispose(ref radixSortImplementation);
            Dispose(ref radixSortPairsImplementation);
        }

        #endregion
    }
}
