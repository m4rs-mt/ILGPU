// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda;

/// <summary>
/// Represents a Cuda stream.
/// </summary>
[SuppressMessage(
    "Microsoft.Naming",
    "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
public sealed class CudaStream : AcceleratorStream
{
    #region Instance

    private IntPtr _streamPtr;
    private readonly bool _responsibleForHandle;

    /// <summary>
    /// Constructs a new Cuda stream from the given native pointer.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="ptr">The native stream pointer.</param>
    /// <param name="responsible">
    /// Whether ILGPU is responsible of disposing this stream.
    /// </param>
    internal CudaStream(Accelerator accelerator, IntPtr ptr, bool responsible)
        : base(accelerator, AcceleratorStreamFlags.None)
    {
        _streamPtr = ptr;
        _responsibleForHandle = responsible;
    }

    /// <summary>
    /// Constructs a new Cuda stream with given <see cref="StreamFlags"/>.
    /// </summary>
    /// <param name="accelerator">The associated accelerator.</param>
    /// <param name="flags">
    /// Stream flags to use. Allows blocking and non-blocking streams.
    /// </param>
    internal CudaStream(Accelerator accelerator, AcceleratorStreamFlags flags)
        : base(accelerator, flags)
    {
        CudaException.ThrowIfFailed(
            CurrentAPI.CreateStream(
                out _streamPtr,
                (flags & AcceleratorStreamFlags.Async) == AcceleratorStreamFlags.Async
                ? StreamFlags.CU_STREAM_NON_BLOCKING
                : StreamFlags.CU_STREAM_DEFAULT));
        _responsibleForHandle = true;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the underlying native Cuda stream.
    /// </summary>
    public IntPtr StreamPtr => _streamPtr;

    #endregion

    #region Methods

    /// <summary cref="AcceleratorStream.Synchronize"/>
    public override void Synchronize()
    {
        var binding = Accelerator.AsNotNull().BindScoped();

        CudaException.ThrowIfFailed(
            CurrentAPI.SynchronizeStream(_streamPtr));

        binding.Recover();
    }

    /// <inheritdoc/>
    protected override ProfilingMarker AddProfilingMarkerInternal()
    {
        var accelerator = Accelerator.AsNotNull();
        using var binding = accelerator.BindScoped();
        var profilingMarker = new CudaProfilingMarker(accelerator);

        CudaException.ThrowIfFailed(
            CurrentAPI.RecordEvent(profilingMarker.EventPtr, StreamPtr));
        return profilingMarker;
    }


    /// <summary>
    /// Allocates a pitched 2D buffer with X being the leading dimension using an
    /// alignment of <see cref="CudaAccelerator.PitchedAllocationAlignmentInBytes"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="extent">The number of elements to allocate.</param>
    /// <returns>An allocated 2D buffer on this accelerator.</returns>
    /// <remarks>
    /// Since X is the leading dimension, X must be less or equal to
    /// <see cref="int.MaxValue"/>.
    /// </remarks>
    public MemoryBuffer2D<T, Stride2D.DenseX> Allocate2DPitchedX<T>(
        LongIndex2D extent)
        where T : unmanaged =>
        Allocate2DPitchedX<T>(
            extent,
            CudaAccelerator.PitchedAllocationAlignmentInBytes);

    /// <summary>
    /// Allocates a pitched 2D buffer with Y being the leading dimension using an
    /// alignment of <see cref="CudaAccelerator.PitchedAllocationAlignmentInBytes"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="extent">The number of elements to allocate.</param>
    /// <returns>An allocated 2D buffer on this accelerator.</returns>
    /// <remarks>
    /// Since Y is the leading dimension, Y must be less or equal to
    /// <see cref="int.MaxValue"/>.
    /// </remarks>
    public MemoryBuffer2D<T, Stride2D.DenseY> Allocate2DPitchedY<T>(
        LongIndex2D extent)
        where T : unmanaged =>
        Allocate2DPitchedY<T>(
            extent,
            CudaAccelerator.PitchedAllocationAlignmentInBytes);

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes this Cuda stream.
    /// </summary>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        if (!_responsibleForHandle || _streamPtr == IntPtr.Zero)
            return;

        CudaException.VerifyDisposed(
            disposing,
            CurrentAPI.DestroyStream(_streamPtr));
        _streamPtr = IntPtr.Zero;
    }

    #endregion
}
