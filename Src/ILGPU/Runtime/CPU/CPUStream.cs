// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// A synchronous accelerator stream for CPU execution.
/// </summary>
/// <remarks>
/// CPU kernels execute inline during <c>Launch()</c> calls — there is no
/// asynchronous command queue. All operations complete before returning.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Matches base class AcceleratorStream naming convention.")]
public sealed class CPUStream : AcceleratorStream
{
    /// <summary>
    /// Creates a new CPU stream.
    /// </summary>
    /// <param name="accelerator">The parent accelerator.</param>
    /// <param name="flags">Stream creation flags.</param>
    internal CPUStream(Accelerator accelerator, AcceleratorStreamFlags flags)
        : base(accelerator, flags)
    { }

    /// <inheritdoc/>
    public override void Synchronize()
    {
        // No-op: CPU execution is synchronous.
    }

    /// <inheritdoc/>
    protected override ProfilingMarker AddProfilingMarkerInternal() =>
        new CPUProfilingMarker(Accelerator!);

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing) { }
}

/// <summary>
/// A simple timestamp-based profiling marker for CPU execution.
/// </summary>
internal sealed class CPUProfilingMarker : ProfilingMarker
{
    private readonly long _timestamp = Stopwatch.GetTimestamp();

    internal CPUProfilingMarker(Accelerator accelerator) : base(accelerator) { }

    /// <inheritdoc/>
    public override void Synchronize()
    {
        // No-op: CPU execution is synchronous.
    }

    /// <inheritdoc/>
    public override TimeSpan MeasureFrom(ProfilingMarker marker)
    {
        if (marker is not CPUProfilingMarker cpuMarker)
        {
            throw new ArgumentException(
                "Cannot measure between different profiling marker types.",
                nameof(marker));
        }
        long elapsed = _timestamp - cpuMarker._timestamp;
        return TimeSpan.FromTicks(
            elapsed * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
    }

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing) { }
}
