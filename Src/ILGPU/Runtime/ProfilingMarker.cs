// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ProfilingMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime;

/// <summary>
/// Represents a point-in-time marker used in profiling.
/// </summary>
/// <param name="accelerator">The associated accelerator.</param>
public abstract class ProfilingMarker(Accelerator accelerator) :
    AcceleratorObject(accelerator)
{
    /// <summary>
    /// Waits for the profiling marker to complete.
    /// </summary>
    public abstract void Synchronize();

    /// <summary>
    /// Returns the elapsed time from this profiling marker to the given marker.
    /// </summary>
    /// <param name="marker">The comparison profiling marker.</param>
    /// <returns>The elapsed time.</returns>
    /// <remarks>Will block until the profiling markers have completed.</remarks>
    public abstract TimeSpan MeasureFrom(ProfilingMarker marker);

    /// <summary>
    /// Returns the elapsed time between two profiling markers.
    /// </summary>
    /// <param name="end">The end profiling marker.</param>
    /// <param name="start">The start profiling marker.</param>
    /// <returns>The elapsed time.</returns>
    /// <remarks>Will block until the profiling markers has completed.</remarks>
    public static TimeSpan operator -(ProfilingMarker end, ProfilingMarker start) =>
        end.MeasureFrom(start);
}
