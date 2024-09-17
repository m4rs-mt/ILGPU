// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaStreamMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a marker used in CUDA streams.
    /// </summary>
    internal sealed class CudaStreamMarker : StreamMarker
    {
        #region Instance

        internal CudaStreamMarker(Accelerator accelerator)
            : base (accelerator)
        {
            CudaException.ThrowIfFailed(
                CurrentAPI.CreateEvent(
                    out var eventPtr,
                    CudaEventFlags.CU_EVENT_DEFAULT));
            EventPtr = eventPtr;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The native event pointer.
        /// </summary>
        public IntPtr EventPtr { get; private set; }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void Synchronize()
        {
            using var binding = Accelerator.BindScoped();

            var errorStatus = CurrentAPI.QueryEvent(EventPtr);
            if (errorStatus == CudaError.CUDA_ERROR_NOT_READY)
                CudaException.ThrowIfFailed(CurrentAPI.SynchronizeEvent(EventPtr));
            else
                CudaException.ThrowIfFailed(errorStatus);
        }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.DestroyEvent(EventPtr));
            EventPtr = IntPtr.Zero;
        }

        /// <inheritdoc/>
        public unsafe override void Record(AcceleratorStream stream)
        {
            if (stream is not CudaStream cudaStream)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedAcceleratorStream);
            }

            CudaException.ThrowIfFailed(
                CurrentAPI.RecordEvent(EventPtr, cudaStream.StreamPtr));
        }

        #endregion
    }
}
