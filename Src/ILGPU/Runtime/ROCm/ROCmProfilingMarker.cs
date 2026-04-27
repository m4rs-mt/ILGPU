// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmProfilingMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// A host-side profiling marker for ROCm streams.
/// Uses <see cref="Stopwatch"/> timestamps since HIP events are not yet integrated.
/// </summary>
sealed class ROCmProfilingMarker : ProfilingMarker
{
    private readonly long _timestamp;

    internal ROCmProfilingMarker(Accelerator accelerator)
        : base(accelerator)
    {
        _timestamp = Stopwatch.GetTimestamp();
    }

    /// <inheritdoc/>
    public override void Synchronize()
    {
        // Host-side marker — nothing to synchronize.
    }

    /// <inheritdoc/>
    public override TimeSpan MeasureFrom(ProfilingMarker marker)
    {
        if (marker is not ROCmProfilingMarker startMarker)
        {
            throw new ArgumentException(
                "The profiling marker must be a ROCm profiling marker.",
                nameof(marker));
        }

        long ticks = _timestamp - startMarker._timestamp;
        return Stopwatch.GetElapsedTime(0, ticks);
    }

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        // No native resources to release.
    }
}
