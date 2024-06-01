// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLStreamMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents a marker used in OpenCL streams.
    /// </summary>
    internal sealed class CLStreamMarker : StreamMarker
    {
        #region Instance

        internal CLStreamMarker(Accelerator accelerator)
            : base(accelerator)
        {
            EventPtr = IntPtr.Zero;
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
        public unsafe override void Synchronize()
        {
            if (EventPtr == IntPtr.Zero)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedAcceleratorStreamMarker);
            }

            using var binding = Accelerator.BindScoped();

            ReadOnlySpan<IntPtr> events = stackalloc[] { EventPtr };
            CLException.ThrowIfFailed(
                CurrentAPI.WaitForEvents(events));
        }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            CLException.VerifyDisposed(
                disposing,
                CurrentAPI.clReleaseEvent(EventPtr));
            EventPtr = IntPtr.Zero;
        }

        /// <inheritdoc/>
        public unsafe override void Record(AcceleratorStream stream)
        {
            if (stream is not CLStream clStream)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedAcceleratorStream);
            }

            // If we have a previously recorded event, discard it.
            if (EventPtr != IntPtr.Zero)
            {
                CLException.ThrowIfFailed(CurrentAPI.clReleaseEvent(EventPtr));
                EventPtr = IntPtr.Zero;
            }

            // Waits for all previously enqueued commands to finish, and then completes
            // the event. We can therefore use the event as a marker to record the current
            // state of the queue.
            IntPtr* streamEvent = stackalloc IntPtr[1];
            CLException.ThrowIfFailed(
                CurrentAPI.EnqueueBarrierWithWaitList(
                    clStream.CommandQueue,
                    Array.Empty<IntPtr>(),
                    streamEvent));
            EventPtr = streamEvent[0];
        }

        #endregion
    }
}
