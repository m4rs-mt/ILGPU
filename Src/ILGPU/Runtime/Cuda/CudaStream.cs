// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda stream.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    public sealed class CudaStream : AcceleratorStream
    {
        #region Instance

        private IntPtr streamPtr;
        private readonly bool responsibleForHandle;

        /// <summary>
        /// Constructs a new cuda stream from the given native pointer.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="ptr">The native stream pointer.</param>
        /// <param name="responsible">
        /// Whether ILGPU is responsible of disposing this stream.
        /// </param>
        internal CudaStream(Accelerator accelerator, IntPtr ptr, bool responsible)
            : base(accelerator)
        {
            streamPtr = ptr;
            responsibleForHandle = responsible;
        }

        /// <summary>
        /// Constructs a new cuda stream with given <see cref="StreamFlags"/>
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="flag">
        /// Stream flag to use. Allows blocking and non-blocking streams.
        /// </param>
        internal CudaStream(Accelerator accelerator, StreamFlags flag)
            : base(accelerator)
        {
            CudaException.ThrowIfFailed(
                CudaAPI.Current.CreateStream(
                    out streamPtr, flag));
            responsibleForHandle = true;
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
            var binding = Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CudaAPI.Current.SynchronizeStream(streamPtr));

            binding.Recover();
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (responsibleForHandle)
            {
                if (streamPtr != IntPtr.Zero)
                {
                    CudaException.ThrowIfFailed(
                        CudaAPI.Current.DestroyStream(streamPtr));
                    streamPtr = IntPtr.Zero;
                }
                base.Dispose(disposing);
            }
        }

        #endregion
    }
}
