// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ScanProvider.cs
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
    /// Represents a platform and accelerator-specific implementation of a scan operation.
    /// </summary>
    internal abstract partial class ScanProviderImplementation : LightningObject
    {
        #region Instance

        protected ScanProviderImplementation(Accelerator accelerator)
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
    /// Represents a scan provider for a scan operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed partial class ScanProvider : LightningObject
    {
        #region Instance

        private MemoryBufferCache bufferCache;
        private ScanProviderImplementation implementation;

        internal ScanProvider(Accelerator accelerator, ScanProviderImplementation implementation)
            : base(accelerator)
        {
            bufferCache = new MemoryBufferCache(Accelerator);
            this.implementation = implementation;
        }

        #endregion

        #region Methods

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
    /// Scan functionality for accelerators.
    /// </summary>
    public static partial class ScanExtensions
    {
        #region Scan

        /// <summary>
        /// Extension provider for scan extensions
        /// </summary>
        partial struct ScanExtension : IAcceleratorExtensionProvider<ScanProviderImplementation>
        { }

        /// <summary>
        /// Creates a new provider implementation for scan.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        internal static ScanProviderImplementation CreateScanProviderImplementation(this Accelerator accelerator)
        {
            return accelerator.CreateExtension<ScanProviderImplementation, ScanExtension>(
                new ScanExtension());
        }

        /// <summary>
        /// Creates a new specialized scan provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a construction method")]
        public static ScanProvider CreateScanProvider(this Accelerator accelerator)
        {
            if (accelerator == null)
                throw new ArgumentNullException(nameof(accelerator));
            return new ScanProvider(accelerator, accelerator.CreateScanProviderImplementation());
        }

        #endregion
    }
}
