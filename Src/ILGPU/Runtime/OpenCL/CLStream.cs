// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL;

/// <summary>
/// Represents an OpenCL stream.
/// </summary>
[SuppressMessage(
    "Microsoft.Naming",
    "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
public sealed class CLStream : AcceleratorStream
{
    #region Instance

    private IntPtr queuePtr;
    private readonly bool responsibleForHandle;

    internal CLStream(Accelerator accelerator, IntPtr ptr, bool responsible)
        : base(accelerator, AcceleratorStreamFlags.None)
    {
        queuePtr = ptr;
        responsibleForHandle = responsible;
    }

    internal CLStream(CLAccelerator accelerator, AcceleratorStreamFlags flags)
        : base(accelerator, flags)
    {
        CLCommandQueueProperties properties =
            accelerator.Context.Properties.EnableProfiling
            ? CLCommandQueueProperties.CL_QUEUE_PROFILING_ENABLE
            : (flags & AcceleratorStreamFlags.Async) == AcceleratorStreamFlags.Async
            ? CLCommandQueueProperties.CL_QUEUE_OUT_OF_ORDER_EXEC_MODE_ENABLE
            : default;
        CLException.ThrowIfFailed(
            CurrentAPI.CreateCommandQueue(
                accelerator.PlatformVersion,
                accelerator.DeviceId,
                accelerator.NativePtr,
                properties,
                out queuePtr));
        responsibleForHandle = true;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the native OpenCL command queue.
    /// </summary>
    public IntPtr CommandQueue => queuePtr;

    #endregion

    #region Methods

    /// <summary cref="AcceleratorStream.Synchronize"/>
    public override void Synchronize() =>
        CLException.ThrowIfFailed(
            CurrentAPI.FinishCommandQueue(queuePtr));

    /// <inheritdoc/>
    protected unsafe override ProfilingMarker AddProfilingMarkerInternal()
    {
        var accelerator = Accelerator.AsNotNull();
        using var binding = accelerator.BindScoped();

        IntPtr* profilingEvent = stackalloc IntPtr[1];
        CLException.ThrowIfFailed(
            CurrentAPI.EnqueueBarrierWithWaitList(queuePtr, [], profilingEvent));

        // WORKAROUND: The OpenCL event needs to be awaited now, otherwise
        // it does not contain the correct timing - it appears to have the timing
        // of whenever it gets awaited.
        var marker = new CLProfilingMarker(accelerator, *profilingEvent);
        marker.Synchronize();
        return marker;
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes this OpenCL stream.
    /// </summary>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        if (!responsibleForHandle || queuePtr == IntPtr.Zero)
            return;

        CLException.VerifyDisposed(
            disposing,
            CurrentAPI.ReleaseCommandQueue(queuePtr));
        queuePtr = IntPtr.Zero;
    }

    #endregion
}
