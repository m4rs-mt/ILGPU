// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLProfilingMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents a point-in-time marker used in OpenCL profiling.
    /// </summary>
    internal sealed class CLProfilingMarker : ProfilingMarker
    {
        #region Instance

        internal CLProfilingMarker(IntPtr eventPtr)
        {
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
        public unsafe override void Synchronize()
        {
            ReadOnlySpan<IntPtr> events = stackalloc[] { EventPtr };
            CLException.ThrowIfFailed(
                CurrentAPI.WaitForEvents(events));
        }

        /// <inheritdoc/>
        public override TimeSpan MeasureFrom(ProfilingMarker marker)
        {
            if (!(marker is CLProfilingMarker startMarker))
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

            CLException.ThrowIfFailed(
                CurrentAPI.GetEventProfilingInfo(
                    EventPtr,
                    CLProfilingInfo.CL_PROFILING_COMMAND_END,
                    out var endNanoseconds));
            CLException.ThrowIfFailed(
                CurrentAPI.GetEventProfilingInfo(
                    startMarker.EventPtr,
                    CLProfilingInfo.CL_PROFILING_COMMAND_END,
                    out var startNanoseconds));

            // TimeSpan tracks time in ticks, where a single tick represents one hundred
            // nanoseconds, so we need to convert our elasped nanoseconds into ticks.
            //
            // NB: If the start time is later than the end time, reverse the calculation,
            // and then restore the correct signed result.
            bool swapped = false;
            if (endNanoseconds < startNanoseconds)
            {
                Utilities.Swap(ref startNanoseconds, ref endNanoseconds);
                swapped = true;
            }
            var elapsedNanoseconds = endNanoseconds - startNanoseconds;
            var ticks = (long)(elapsedNanoseconds / 100UL);
            if (swapped)
                ticks = -ticks;

            return new TimeSpan(ticks);
        }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            CLException.VerifyDisposed(
                disposing,
                CurrentAPI.clReleaseEvent(EventPtr));
            EventPtr = IntPtr.Zero;
        }

        #endregion
    }
}
