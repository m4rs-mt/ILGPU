// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ScanProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a platform and accelerator-specific implementation of a scan operation.
    /// </summary>
    internal abstract partial class ScanProviderImplementation : LightningContextObject
    {
        #region Instance

        protected ScanProviderImplementation(LightningContext lightningContext)
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
    /// Represents a scan provider for a scan operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed partial class ScanProvider : LightningContextObject
    {
        #region Instance

        private MemoryBufferCache bufferCache;
        private ScanProviderImplementation implementation;

        internal ScanProvider(LightningContext lightningContext, ScanProviderImplementation implementation)
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
        /// Internal scan implementation.
        /// </summary>
        private ScanProviderImplementation scanImplementation;

        /// <summary>
        /// Initializes the scan implementation
        /// </summary>
        private void InitScan()
        {
            scanImplementation = CreateScanProviderImplementation();
        }

        /// <summary>
        /// Disposes the scan implementation.
        /// </summary>
        private void DisposeScan()
        {
            Dispose(ref scanImplementation);
        }

        #endregion

        #region Scan

        /// <summary>
        /// Extension provider for scan extensions
        /// </summary>
        partial struct ScanExtension : IAcceleratorExtensionProvider<ScanProviderImplementation>
        {
            public ScanExtension(LightningContext lightningContext)
            {
                LightningContext = lightningContext;
            }

            /// <summary>
            /// Returns the current lightning context.
            /// </summary>
            public LightningContext LightningContext { get; }
        }

        /// <summary>
        /// Creates a new provider implementation for scan.
        /// </summary>
        /// <returns>The created scan provider.</returns>
        private ScanProviderImplementation CreateScanProviderImplementation()
        {
            return Accelerator.CreateExtension<ScanProviderImplementation, ScanExtension>(
                new ScanExtension(this));
        }

        /// <summary>
        /// Creates a new specialized scan provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <returns>The created provider.</returns>
        public ScanProvider CreateScanProvider()
        {
            return new ScanProvider(this, CreateScanProviderImplementation());
        }

        #endregion
    }
}
