﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLStream.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Runtime.OpenCL.API;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an OpenCL stream.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    public sealed class CLStream : AcceleratorStream
    {
        #region Instance

        private IntPtr queuePtr;

        internal CLStream(CLAccelerator accelerator)
            : base(accelerator)
        {
            CLException.ThrowIfFailed(
                CLAPI.CreateCommandQueue(
                    accelerator.DeviceId,
                    accelerator.ContextPtr,
                    out queuePtr));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the native OpenCL command queue.
        /// </summary>
        public IntPtr CommandQueue => queuePtr;

        #endregion

        #region Methods

        /// <summary cref="AcceleratorStream.Synchronize"/>
        public override void Synchronize()
        {
            CLException.ThrowIfFailed(
                CLAPI.FinishCommandQueue(queuePtr));
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (queuePtr != IntPtr.Zero)
            {
                CLException.ThrowIfFailed(
                    CLAPI.ReleaseCommandQueue(queuePtr));
                queuePtr = IntPtr.Zero;
            }
        }

        #endregion
    }
}
