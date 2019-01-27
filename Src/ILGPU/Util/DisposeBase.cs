// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: DisposeBase.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Threading;

namespace ILGPU.Util
{
    /// <summary>
    /// Utility base class for correct implementations of IDisposable
    /// </summary>
    public abstract class DisposeBase : IDisposable
    {
        private volatile int disposeBarrier = 0;

        /// <summary>
        /// Triggers the 'dispose' functionality of this object.
        /// </summary>
        public void Dispose()
        {
            DisposeDriver(true);
            GC.SuppressFinalize(this);
        }

       /// <summary>
       /// The custom finalizer for dispose-base objects.
       /// </summary>
        ~DisposeBase()
        {
            DisposeDriver(false);
        }

        /// <summary>
        /// Thread-safe wrapper for the actual dispose functionality.
        /// </summary>
        /// <param name="disposing">True, iff the method is not called by the finalizer.</param>
        private void DisposeDriver(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposeBarrier, 1, 0) != 0)
                return;
            Dispose(disposing);
        }

        /// <summary>
        /// Frees allocated resources.
        /// </summary>
        /// <param name="disposing">True, iff the method is not called by the finalizer.</param>
        protected abstract void Dispose(bool disposing);

        /// <summary>
        /// Disposes the given object and sets its object reference to null.
        /// </summary>
        /// <typeparam name="T">The type of the object to dispose.</typeparam>
        /// <param name="object">The object to dispose.</param>
        public static void Dispose<T>(ref T @object)
            where T : class, IDisposable
        {
            @object?.Dispose();
            @object = null;
        }
    }
}
