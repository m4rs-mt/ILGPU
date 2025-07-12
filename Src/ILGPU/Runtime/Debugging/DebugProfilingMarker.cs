// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugProfilingMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Debugging;

/// <summary>
/// Represents a point-in-time marker used in CPU profiling.
/// </summary>
sealed class DebugProfilingMarker : ProfilingMarker
{
    #region Instance

    internal DebugProfilingMarker(Accelerator accelerator)
        : base(accelerator)
    {
        Timestamp = DateTime.UtcNow;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The timestamp this profiling marker was created.
    /// </summary>
    public DateTime Timestamp { get; private set; }

    #endregion

    #region Methods

    /// <inheritdoc/>
    public override void Synchronize() { }

    /// <inheritdoc/>
    public override TimeSpan MeasureFrom(ProfilingMarker marker)
    {
        using var binding = Accelerator.AsNotNull().BindScoped();

        return (marker is DebugProfilingMarker startMarker)
            ? Timestamp - startMarker.Timestamp
            : throw new ArgumentException(
                string.Format(
                    RuntimeErrorMessages.InvalidProfilingMarker,
                    GetType().Name,
                    marker.GetType().Name),
                nameof(marker));
    }

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing) { }

    #endregion
}
