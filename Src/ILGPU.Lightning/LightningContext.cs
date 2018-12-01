// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: LightningContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents a wrapper object for accelerator instances.
    /// </summary>
    internal sealed partial class LightningContext : LightningObject
    {
        #region Static

        /// <summary>
        /// The current cache lock.
        /// </summary>
        private static readonly ReaderWriterLockSlim readerWriterLock;

        /// <summary>
        /// Represents a cache for lightning contexts.
        /// </summary>
        private static readonly Dictionary<Accelerator, LightningContext> cache;

        static LightningContext()
        {
            cache = new Dictionary<Accelerator, LightningContext>();
            readerWriterLock = new ReaderWriterLockSlim();
        }

        /// <summary>
        /// Dispose callback that is invoked by every accelerator.
        /// </summary>
        /// <param name="sender">The sender (the accelerator).</param>
        /// <param name="e">The event args (not used)</param>
        private static void DisposedCallback(object sender, EventArgs e)
        {
            if (!(sender is Accelerator accelerator))
                return;
            accelerator.Disposed -= DisposedCallback;

            readerWriterLock.EnterWriteLock();
            try
            {
                if (!cache.TryGetValue(accelerator, out LightningContext lc))
                    return;
                cache.Remove(accelerator);
                lc.Dispose();
            }
            finally
            {
                readerWriterLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Resolves the lightning context for the given accelerator.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The associated lightning context.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Object references will be stored in a local cache")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LightningContext Get(Accelerator accelerator)
        {
            Debug.Assert(accelerator != null, "Invalid accelerator");

            readerWriterLock.EnterUpgradeableReadLock();
            try
            {
                if (!cache.TryGetValue(accelerator, out LightningContext result))
                {
                    readerWriterLock.EnterWriteLock();
                    try
                    {
                        result = new LightningContext(accelerator);
                        accelerator.Disposed += DisposedCallback;
                        cache.Add(accelerator, result);
                    }
                    finally
                    {
                        readerWriterLock.ExitWriteLock();
                    }
                }
                return result;
            }
            finally
            {
                readerWriterLock.ExitUpgradeableReadLock();
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new lightning context.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        private LightningContext(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing) { }

        #endregion
    }
}
