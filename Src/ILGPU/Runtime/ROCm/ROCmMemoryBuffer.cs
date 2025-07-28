// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using static ILGPU.Runtime.ROCm.ROCmAPI;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Represents an unmanaged ROCm/HIP GPU memory buffer.
/// </summary>
public sealed class ROCmMemoryBuffer : MemoryBuffer
{
    #region Static

    /// <summary>
    /// Performs a ROCm memset operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The ROCm stream to use (can be null).</param>
    /// <param name="value">The value to write into the buffer.</param>
    /// <param name="targetView">The target view to write to.</param>
    public static void ROCmMemSet<T>(
        ROCmStream? stream,
        byte value,
        in ArrayView<T> targetView)
        where T : unmanaged
    {
        if (targetView.GetAcceleratorType() != AcceleratorType.ROCm)
        {
            throw new NotSupportedException(
                RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }

        var streamHandle = stream?.StreamHandle ?? IntPtr.Zero;
        ROCmException.ThrowIfFailed(
            CurrentAPI.MemsetAsync(
                targetView.LoadEffectiveAddressAsPtr(),
                value,
                new IntPtr(targetView.LengthInBytes),
                streamHandle));
    }

    /// <summary>
    /// Performs a ROCm copy operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The ROCm stream to use (can be null).</param>
    /// <param name="sourceView">The source view to copy from.</param>
    /// <param name="targetView">The target view to copy to.</param>
    public static void ROCmCopy<T>(
        ROCmStream? stream,
        in ArrayView<T> sourceView,
        in ArrayView<T> targetView)
        where T : unmanaged
    {
        var streamHandle = stream?.StreamHandle ?? IntPtr.Zero;
        ROCmException.ThrowIfFailed(
            CurrentAPI.MemcpyAsync(
                targetView.LoadEffectiveAddressAsPtr(),
                sourceView.LoadEffectiveAddressAsPtr(),
                new IntPtr(targetView.LengthInBytes),
                HipMemcpyKind.Default,
                streamHandle));
    }

    #endregion

    #region Instance

    /// <summary>
    /// Constructs a new ROCm memory buffer.
    /// </summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="length">The length of this buffer.</param>
    /// <param name="elementSize">The element size.</param>
    internal ROCmMemoryBuffer(
        ROCmAccelerator accelerator,
        long length,
        int elementSize)
        : base(accelerator, length, elementSize)
    {
        if (LengthInBytes == 0)
        {
            NativePtr = IntPtr.Zero;
        }
        else
        {
            ROCmException.ThrowIfFailed(
                CurrentAPI.Malloc(LengthInBytes, out IntPtr resultPtr));
            NativePtr = resultPtr;
        }
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected internal override void MemSet(
        AcceleratorStream stream,
        byte value,
        in ArrayView<byte> targetView) =>
        ROCmMemSet(stream as ROCmStream, value, targetView);

    /// <inheritdoc/>
    protected internal override void CopyFrom(
        AcceleratorStream stream,
        in ArrayView<byte> sourceView,
        in ArrayView<byte> targetView) =>
        ROCmCopy(stream as ROCmStream, sourceView, targetView);

    /// <inheritdoc/>
    protected internal override void CopyTo(
        AcceleratorStream stream,
        in ArrayView<byte> sourceView,
        in ArrayView<byte> targetView) =>
        ROCmCopy(stream as ROCmStream, sourceView, targetView);

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        CurrentAPI.Free(NativePtr);
        NativePtr = IntPtr.Zero;
    }

    #endregion
}
