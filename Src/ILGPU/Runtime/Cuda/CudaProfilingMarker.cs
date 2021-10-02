// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaProfilingMarker.cs
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
    /// Represents a point-in-time marker used in CUDA profiling.
    /// </summary>
    internal sealed class CudaProfilingMarker : ProfilingMarker
    {
        #region Instance

        internal CudaProfilingMarker()
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
            var errorStatus = CurrentAPI.QueryEvent(EventPtr);
            if (errorStatus == CudaError.CUDA_ERROR_NOT_READY)
                CudaException.ThrowIfFailed(CurrentAPI.SynchronizeEvent(EventPtr));
            else
                CudaException.ThrowIfFailed(errorStatus);
        }

        /// <inheritdoc/>
        public override TimeSpan MeasureFrom(ProfilingMarker marker)
        {
            if (!(marker is CudaProfilingMarker startMarker))
            {
                throw new ArgumentException(
                    string.Format(
                        RuntimeErrorMessages.InvalidProfilingMarker,
                        GetType().Name,
                        marker.GetType().Name),
                    nameof(marker));
            }

            // Wait for the markers to complete, then calculate the duration.
            startMarker.Synchronize();
            Synchronize();

            CudaException.ThrowIfFailed(
                CurrentAPI.ElapsedTime(
                    out float milliseconds,
                    startMarker.EventPtr,
                    EventPtr));
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.DestroyEvent(EventPtr));
            EventPtr = IntPtr.Zero;
        }

        #endregion
    }
}
