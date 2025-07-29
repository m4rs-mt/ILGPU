// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using static ILGPU.Runtime.ROCm.ROCmAPI;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Represents a HIP/ROCm stream backed by a hipStream_t.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Matches base class AcceleratorStream naming convention.")]
public sealed class ROCmStream : AcceleratorStream
{
    private IntPtr _streamHandle;
    private readonly bool _responsibleForHandle;

    /// <summary>
    /// Creates a new ROCm stream from an existing HIP stream handle.
    /// </summary>
    /// <param name="accelerator">The parent accelerator.</param>
    /// <param name="streamHandle">The native HIP stream handle.</param>
    /// <param name="responsible">
    /// Whether this stream owns the native handle and should destroy it on disposal.
    /// </param>
    internal ROCmStream(
        Accelerator accelerator,
        IntPtr streamHandle,
        bool responsible)
        : base(accelerator, AcceleratorStreamFlags.None)
    {
        _streamHandle = streamHandle;
        _responsibleForHandle = responsible;
    }

    /// <summary>
    /// Creates a new ROCm stream by creating a new HIP stream.
    /// </summary>
    /// <param name="accelerator">The parent ROCm accelerator.</param>
    /// <param name="flags">Stream creation flags.</param>
    internal ROCmStream(
        ROCmAccelerator accelerator,
        AcceleratorStreamFlags flags)
        : base(accelerator, flags)
    {
        uint hipFlags =
            (flags & AcceleratorStreamFlags.Async) == AcceleratorStreamFlags.Async
            ? ROCmAPI.HipStreamNonBlocking
            : ROCmAPI.HipStreamDefault;

        ROCmException.ThrowIfFailed(
            CurrentAPI.StreamCreateWithFlags(out _streamHandle, hipFlags));
        _responsibleForHandle = true;
    }

    /// <summary>
    /// Returns the native HIP stream handle.
    /// </summary>
    public IntPtr StreamHandle => _streamHandle;

    /// <inheritdoc/>
    public override void Synchronize()
    {
        var binding = Accelerator.AsNotNull().BindScoped();

        ROCmException.ThrowIfFailed(
            CurrentAPI.StreamSynchronize(_streamHandle));

        binding.Recover();
    }

    /// <inheritdoc/>
    protected override ProfilingMarker AddProfilingMarkerInternal() =>
        new ROCmProfilingMarker(Accelerator!);

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        if (!_responsibleForHandle || _streamHandle == IntPtr.Zero)
            return;

        CurrentAPI.StreamDestroy(_streamHandle);
        _streamHandle = IntPtr.Zero;
    }
}
