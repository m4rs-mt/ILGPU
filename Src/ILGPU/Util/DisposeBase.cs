// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: DisposeBase.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Util
{
    /// <summary>
    /// Utility base class for correct implementations of IDisposable
    /// </summary>
    public abstract class DisposeBase : IDisposable
    {
        /// <summary>
        /// Triggers the 'dispose' functionality of this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

       /// <summary>
       /// The custom finalizer for dispose-base objects.
       /// </summary>
        ~DisposeBase()
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
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
