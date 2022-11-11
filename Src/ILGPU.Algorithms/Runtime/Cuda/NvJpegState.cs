// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvJpegState.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an NvJpeg state.
    /// </summary>
    [CLSCompliant(false)]
    public sealed partial class NvJpegState : DisposeBase
    {
        /// <summary>
        /// Constructs a new instance to wrap an NvJpeg state.
        /// </summary>
        public NvJpegState(NvJpegAPI api, IntPtr stateHandle)
        {
            API = api;
            StateHandle = stateHandle;
        }

        /// <summary>
        /// The underlying API wrapper.
        /// </summary>
        public NvJpegAPI API { get; }

        /// <summary>
        /// The native handle.
        /// </summary>
        public IntPtr StateHandle { get; private set; }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                NvJpegException.ThrowIfFailed(
                    API.JpegStateDestroy(StateHandle));
                StateHandle = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }
    }
}
