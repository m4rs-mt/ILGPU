// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ProfilingMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a point-in-time marker used in profiling.
    /// </summary>
    public abstract class ProfilingMarker : AcceleratorObject
    {
        /// <summary>
        /// Waits for the profiling marker to complete.
        /// </summary>
        public abstract void Synchronize();

        /// <summary>
        /// Returns the elapsed time from this profiling marker to the given marker.
        /// </summary>
        /// <param name="marker">The comparison profiing marker.</param>
        /// <returns>The elapsed time.</returns>
        /// <remarks>Will block until the profiling markers have completed.</remarks>
        public abstract TimeSpan MeasureFrom(ProfilingMarker marker);

        /// <summary>
        /// Returns the elapsed time between two profiling markers.
        /// </summary>
        /// <param name="end">The end profiing marker.</param>
        /// <param name="start">The start profiing marker.</param>
        /// <returns>The elapsed time.</returns>
        /// <remarks>Will block until the profiling markers has completed.</remarks>
        public static TimeSpan operator -(ProfilingMarker end, ProfilingMarker start) =>
            end.MeasureFrom(start);
    }

    /// <summary>
    /// Profiling marker extensions for accelerators.
    /// </summary>
    public static class ProfilingMarkers
    {
        /// <summary>
        /// Adds a profiling marker to the accelerator default stream.
        /// </summary>
        public static ProfilingMarker AddProfilingMarker(this Accelerator accelerator) =>
            accelerator.DefaultStream.AddProfilingMarker();
    }
}
