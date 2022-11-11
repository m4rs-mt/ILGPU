// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: DisposeBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ILGPU.Util
{
    /// <summary>
    /// Utility base class for correct implementations of IDisposable
    /// </summary>
    public abstract class DisposeBase : IDisposable
    {
        #region Instance

        /// <summary>
        /// A synchronization primitive to avoid multiple concurrent invocations of the
        /// <see cref="Dispose(bool)"/> function.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private volatile int disposeBarrier;

        /// <summary>
        /// Tracks whether the object has been disposed.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private volatile bool isDisposed;

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if the current object has been disposed
        /// </summary>
        [DebuggerHidden]
        public bool IsDisposed => isDisposed;

        #endregion

        #region Methods

        /// <summary>
        /// Verifies if the current instance is not disposed and still alive. If the
        /// current object has been disposed, this method throws a
        /// <see cref="ObjectDisposedException"/>.
        /// </summary>
        /// <remarks>
        /// This method has been added for general utility purposes and is not used.
        /// </remarks>
        protected void VerifyNotDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        #endregion

        #region Dispose Methods

        /// <summary>
        /// Thread-safe wrapper for the actual dispose functionality.
        /// </summary>
        /// <param name="disposing">
        /// True, if the method is not called by the finalizer.
        /// </param>
        private void DisposeDriver(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposeBarrier, 1, 0) != 0)
                return;
            Dispose(disposing);
            isDisposed = true;
        }

        /// <summary>
        /// Frees allocated resources.
        /// </summary>
        /// <param name="disposing">
        /// True, if the method is not called by the finalizer.
        /// </param>
        protected virtual void Dispose(bool disposing) { }

        /// <summary>
        /// Marks the current object as disposed.
        /// </summary>
        /// <returns>Return true if the object has not been disposed.</returns>
        protected bool MarkDisposed_Unsafe()
        {
            bool value = isDisposed;
            isDisposed = true;
            return !value;
        }

        #endregion

        #region IDisposable & Finalizer

        /// <summary>
        /// Triggers the 'dispose' functionality of this object.
        /// </summary>
        [SuppressMessage(
            "Design",
            "CA1063:Implement IDisposable Correctly",
            Justification = "We use a custom thread-safe dispose driver")]
        public void Dispose()
        {
            // Suppress the invocation of the finalizer first since the internal
            // dispose functionality will be executed anyway right now
            GC.SuppressFinalize(this);
            DisposeDriver(true);
        }

        /// <summary>
        /// The custom finalizer for dispose-base objects.
        /// </summary>
        [SuppressMessage(
            "Design",
            "CA1063:Implement IDisposable Correctly",
            Justification = "We use a custom thread-safe dispose driver")]
        ~DisposeBase()
        {
            DisposeDriver(false);
        }

        #endregion
    }
}
