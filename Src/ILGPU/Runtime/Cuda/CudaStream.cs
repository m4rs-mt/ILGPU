// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: CudaStream.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda stream.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    public sealed class CudaStream : AcceleratorStream
    {
        #region Constants

        internal static readonly CudaStream Default = new CudaStream(IntPtr.Zero);

        #endregion

        #region Instance

        private IntPtr streamPtr;

        private CudaStream(IntPtr ptr)
        {
            streamPtr = ptr;
        }

        internal CudaStream()
        {
            CudaException.ThrowIfFailed(
                CudaAPI.Current.CreateStream(out streamPtr, StreamFlags.CU_STREAM_DEFAULT));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying native Cuda stream.
        /// </summary>
        public IntPtr StreamPtr => streamPtr;

        #endregion

        #region Methods

        /// <summary cref="AcceleratorStream.Synchronize"/>
        public override void Synchronize()
        {
            CudaException.ThrowIfFailed(CudaAPI.Current.SynchronizeStream(streamPtr));
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (streamPtr != IntPtr.Zero)
            {
                CudaException.ThrowIfFailed(CudaAPI.Current.DestroyStream(streamPtr));
                streamPtr = IntPtr.Zero;
            }
        }

        #endregion
    }
}
