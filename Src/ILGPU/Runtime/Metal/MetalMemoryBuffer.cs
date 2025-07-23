// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Represents a Metal GPU memory buffer backed by an MTLBuffer with shared
/// storage mode (unified memory on Apple Silicon).
/// </summary>
public sealed class MetalMemoryBuffer : MemoryBuffer
{
    /// <summary>
    /// Returns the native MTLBuffer handle.
    /// </summary>
    internal IntPtr MetalBuffer { get; private set; }

    /// <summary>
    /// Creates a new Metal memory buffer.
    /// </summary>
    /// <param name="accelerator">The parent Metal accelerator.</param>
    /// <param name="length">Number of elements.</param>
    /// <param name="elementSize">Size of each element in bytes.</param>
    internal MetalMemoryBuffer(
        MetalAccelerator accelerator,
        long length,
        int elementSize)
        : base(accelerator, length, elementSize)
    {
        if (LengthInBytes == 0)
        {
            MetalBuffer = IntPtr.Zero;
            NativePtr = IntPtr.Zero;
        }
        else
        {
            MetalBuffer = MetalAPI.NewBuffer(
                accelerator.DeviceHandle,
                (ulong)LengthInBytes,
                MetalResourceOptions.StorageModeShared);

            MetalException.ThrowIfNull(
                MetalBuffer,
                IntPtr.Zero,
                MetalError.BufferAllocationFailed,
                $"Failed to allocate Metal buffer of {LengthInBytes} bytes");

            // For shared storage mode, contents returns a CPU-accessible pointer
            // that is also accessible by the GPU (unified memory).
            NativePtr = MetalAPI.GetBufferContents(MetalBuffer);
        }
    }

    /// <summary>
    /// Resolves an <see cref="IArrayView"/> to its underlying MTLBuffer handle
    /// and byte offset. Used by generated Metal launcher code to convert
    /// ArrayView parameters into the (buffer, offset) pair required by
    /// <c>[encoder setBuffer:offset:atIndex:]</c>.
    /// </summary>
    /// <param name="view">The array view whose underlying buffer to resolve.</param>
    /// <param name="effectivePtr">
    /// The effective address of the view (from ViewImplementation.Ptr),
    /// used to compute the byte offset within the buffer.
    /// </param>
    /// <param name="mtlBuffer">The MTLBuffer handle that backs the view.</param>
    /// <param name="byteOffset">Byte offset from the start of the buffer.</param>
    public static void ResolveView(
        IArrayView view,
        IntPtr effectivePtr,
        out IntPtr mtlBuffer,
        out ulong byteOffset)
    {
        var metalBuf = (MetalMemoryBuffer)view.Buffer;
        mtlBuffer = metalBuf.MetalBuffer;
        byteOffset = (ulong)((nint)effectivePtr - (nint)metalBuf.NativePtr);
    }

    /// <inheritdoc/>
    protected internal override unsafe void MemSet(
        AcceleratorStream stream,
        byte value,
        in ArrayView<byte> targetView)
    {
        if (targetView.Length == 0)
            return;

        // Synchronize to ensure no in-flight GPU work on this buffer.
        stream.Synchronize();

        var ptr = targetView.LoadEffectiveAddressAsPtr();
        Unsafe.InitBlock(
            ref Unsafe.AsRef<byte>(ptr.ToPointer()),
            value,
            (uint)targetView.Length);
    }

    /// <inheritdoc/>
    protected internal override unsafe void CopyTo(
        AcceleratorStream stream,
        in ArrayView<byte> sourceView,
        in ArrayView<byte> targetView)
    {
        if (sourceView.Length == 0)
            return;

        // Synchronize to ensure GPU is done with the source buffer.
        stream.Synchronize();

        var src = sourceView.LoadEffectiveAddressAsPtr();
        var dst = targetView.LoadEffectiveAddressAsPtr();
        Unsafe.CopyBlock(
            ref Unsafe.AsRef<byte>(dst.ToPointer()),
            ref Unsafe.AsRef<byte>(src.ToPointer()),
            (uint)sourceView.Length);
    }

    /// <inheritdoc/>
    protected internal override unsafe void CopyFrom(
        AcceleratorStream stream,
        in ArrayView<byte> sourceView,
        in ArrayView<byte> targetView)
    {
        if (sourceView.Length == 0)
            return;

        // Synchronize to ensure GPU is done with the target buffer.
        stream.Synchronize();

        var src = sourceView.LoadEffectiveAddressAsPtr();
        var dst = targetView.LoadEffectiveAddressAsPtr();
        Unsafe.CopyBlock(
            ref Unsafe.AsRef<byte>(dst.ToPointer()),
            ref Unsafe.AsRef<byte>(src.ToPointer()),
            (uint)sourceView.Length);
    }

    /// <inheritdoc/>
    protected override void DisposeAcceleratorObject(bool disposing)
    {
        if (MetalBuffer != IntPtr.Zero)
        {
            MetalAPI.Release(MetalBuffer);
            MetalBuffer = IntPtr.Zero;
            NativePtr = IntPtr.Zero;
        }
    }
}
