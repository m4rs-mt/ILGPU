// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

namespace ILGPU.Runtime.Debugging;

/// <summary>
/// Represents a debug stream.
/// </summary>
sealed class DebugStream : AcceleratorStream
{
    #region Instance

    /// <summary>
    /// Constructs a new debug stream.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    internal DebugStream(Accelerator accelerator)
        : base(accelerator, AcceleratorStreamFlags.None)
    { }

    #endregion

    #region Methods

    /// <summary>
    /// Does not perform any operation.
    /// </summary>
    public override void Synchronize() { }

    /// <inheritdoc/>
    protected unsafe override ProfilingMarker AddProfilingMarkerInternal()
    {
        var accelerator = Accelerator.AsNotNull();
        using var binding = accelerator.BindScoped();
        return new DebugProfilingMarker(accelerator);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Does not perform any operation.
    /// </summary>
    protected override void DisposeAcceleratorObject(bool disposing) { }

    #endregion
}

#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
