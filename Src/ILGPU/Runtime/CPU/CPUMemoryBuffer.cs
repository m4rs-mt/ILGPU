// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// A memory buffer that lives in CPU space.
/// </summary>
public abstract partial class CPUMemoryBuffer : MemoryBuffer
{
    #region Static

    /// <summary>
    /// Performs a unsafe memset operation on a CPU memory pointer.
    /// </summary>
    /// <param name="nativePtr">The native pointer to CPU memory.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="offsetInBytes">
    /// The offset in bytes to begin the operation.
    /// </param>
    /// <param name="lengthInBytes">The number of bytes to set.</param>
    public static unsafe void CPUMemSet(
        IntPtr nativePtr,
        byte value,
        long offsetInBytes,
        long lengthInBytes)
    {
        ref byte targetAddress = ref Unsafe.AsRef<byte>(nativePtr.ToPointer());
        for (
            long offset = offsetInBytes, e = offsetInBytes + lengthInBytes;
            offset < e;
            offset += uint.MaxValue)
        {
            Unsafe.InitBlock(
                ref Unsafe.AddByteOffset(
                    ref targetAddress,
                    new IntPtr(offset)),
                value,
                (uint)Math.Min(uint.MaxValue, e - offset));
        }
    }

    /// <summary>
    /// Copies CPU content to a CPU target address.
    /// </summary>
    /// <param name="sourcePtr">The source pointer in CPU address space.</param>
    /// <param name="targetPtr">The target pointer in CPU address space.</param>
    /// <param name="sourceLengthInBytes">
    /// The length of the source buffer in bytes.
    /// </param>
    /// <param name="targetLengthInBytes">
    /// The length of the target buffer in bytes.
    /// </param>
    public static unsafe void CPUCopyToCPU(
        ref byte sourcePtr,
        ref byte targetPtr,
        long sourceLengthInBytes,
        long targetLengthInBytes) =>
        Buffer.MemoryCopy(
            Unsafe.AsPointer(ref sourcePtr),
            Unsafe.AsPointer(ref targetPtr),
            sourceLengthInBytes,
            targetLengthInBytes);

    /// <summary>
    /// Copies CPU data (target view) from the given source view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The current stream.</param>
    /// <param name="sourceView">The source view in some address space.</param>
    /// <param name="targetView">The target view in CPU address space.</param>
    public static void CPUCopyFrom<T>(
        AcceleratorStream stream,
        in ArrayView<T> sourceView,
        in ArrayView<T> targetView)
        where T : unmanaged
    {
        switch (targetView.GetAcceleratorType())
        {
            case AcceleratorType.Debug:
                break;
            default:
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }
        if (sourceView.Length > targetView.Length)
            throw new ArgumentOutOfRangeException(nameof(sourceView));

        // Skip empty buffers
        if (sourceView.HasNoData())
            return;

        switch (sourceView.GetAcceleratorType())
        {
            case AcceleratorType.Debug:
                // Copy from CPU to CPU
                CPUCopyToCPU(
                    ref sourceView.LoadEffectiveAddress(),
                    ref targetView.LoadEffectiveAddress(),
                    sourceView.LengthInBytes,
                    targetView.LengthInBytes);
                break;
            case AcceleratorType.Cuda:
                // Copy from Cuda to CPU
                if (!(stream is CudaStream cudaStream))
                {
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedAcceleratorStream);
                }
                CudaMemoryBuffer.CudaCopy(
                    cudaStream,
                    sourceView,
                    targetView);
                break;
            case AcceleratorType.OpenCL:
                // Copy from OpenCL to CPU
                if (!(stream is CLStream clStream))
                {
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedAcceleratorStream);
                }
                CLMemoryBuffer.CLCopy(
                    clStream,
                    sourceView,
                    targetView);
                break;
            default:
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }
    }

    /// <summary>
    /// Copies data from the source view to a CPU buffer (target view).
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The current stream.</param>
    /// <param name="sourceView">The source view in CPU address space.</param>
    /// <param name="targetView">The target view in some address space.</param>
    public static void CPUCopyTo<T>(
        AcceleratorStream stream,
        in ArrayView<T> sourceView,
        in ArrayView<T> targetView)
        where T : unmanaged
    {
        switch (sourceView.GetAcceleratorType())
        {
            case AcceleratorType.Debug:
                break;
            default:
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }
        if (targetView.Length > sourceView.Length)
            throw new ArgumentOutOfRangeException(nameof(sourceView));

        // Skip empty buffers
        if (targetView.HasNoData())
            return;

        switch (targetView.GetAcceleratorType())
        {
            case AcceleratorType.Debug:
                // Copy from CPU to CPU
                CPUCopyToCPU(
                    ref sourceView.LoadEffectiveAddress(),
                    ref targetView.LoadEffectiveAddress(),
                    sourceView.LengthInBytes,
                    targetView.LengthInBytes);
                break;
            case AcceleratorType.Cuda:
                // Copy from CPU to Cuda
                if (!(stream is CudaStream cudaStream))
                {
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedAcceleratorStream);
                }
                CudaMemoryBuffer.CudaCopy(
                    cudaStream,
                    sourceView,
                    targetView);
                break;
            case AcceleratorType.OpenCL:
                // Copy from CPU to OpenCL
                if (stream is not CLStream clStream)
                {
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedAcceleratorStream);
                }
                CLMemoryBuffer.CLCopy(
                    clStream,
                    sourceView,
                    targetView);
                break;
            default:
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }
    }

    /// <summary>
    /// Copies data from the source view to the target view, where one of the views
    /// has to live in the CPU address space.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="stream">The current stream.</param>
    /// <param name="sourceView">The source view.</param>
    /// <param name="targetView">The target view.</param>
    public static void CPUCopy<T>(
        AcceleratorStream stream,
        in ArrayView<T> sourceView,
        in ArrayView<T> targetView)
        where T : unmanaged
    {
        if (sourceView.GetAcceleratorType() == AcceleratorType.Debug)
        {
            CPUCopyTo(stream, sourceView, targetView);
        }
        else if (targetView.GetAcceleratorType() == AcceleratorType.Debug)
        {
            CPUCopyFrom(stream, sourceView, targetView);
        }
        else
        {
            throw new NotSupportedException(
                RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }
    }

    #endregion

    #region Instance

    /// <summary>
    /// Initializes this array view source on the CPU.
    /// </summary>
    /// <param name="length">The length of this source.</param>
    /// <param name="elementSize">The element size.</param>
    protected internal CPUMemoryBuffer(
        long length,
        int elementSize)
        : base(accelerator: null, length, elementSize)
    { }

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected internal override unsafe void MemSet(
        AcceleratorStream stream,
        byte value,
        in ArrayView<byte> targetView) =>
        CPUMemSet(
            targetView.LoadEffectiveAddressAsPtr(),
            value,
            0L,
            targetView.LengthInBytes);

    /// <inheritdoc/>
    protected internal override void CopyFrom(
        AcceleratorStream stream,
        in ArrayView<byte> sourceView,
        in ArrayView<byte> targetView) =>
        CPUCopyFrom(stream, sourceView, targetView);

    /// <inheritdoc/>
    protected internal override unsafe void CopyTo(
        AcceleratorStream stream,
        in ArrayView<byte> sourceView,
        in ArrayView<byte> targetView) =>
        CPUCopyTo(stream, sourceView, targetView);

    #endregion
}

partial class CPUMemoryBuffer
{
    #region Nested Types

    /// <summary>
    /// Creates a new view pointer wrapper that wraps a pointer reference
    /// inside an array view.
    /// </summary>
    class PointerSourceBuffer : CPUMemoryBuffer
    {
        #region Instance

        /// <summary>
        /// Creates a new pointer wrapper.
        /// </summary>
        /// <param name="ptr">The native value pointer.</param>
        /// <param name="length">The length of this buffer.</param>
        /// <param name="elementSize">The element size.</param>
        internal PointerSourceBuffer(IntPtr ptr, long length, int elementSize)
            : base(length, elementSize)
        {
            NativePtr = ptr;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Does not perform any operation.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing) { }

        #endregion
    }

    /// <summary>
    /// Represents a buffer that allocates native memory in the CPU address space.
    /// </summary>
    class UnmanagedMemoryBuffer : PointerSourceBuffer
    {
        #region Instance

        /// <summary>
        /// Allocates an unmanaged memory buffer on the CPU.
        /// </summary>
        /// <param name="length">The length of this buffer.</param>
        /// <param name="elementSize">The element size.</param>
        internal UnmanagedMemoryBuffer(long length, int elementSize)
            : base(
                  Marshal.AllocHGlobal(new IntPtr(length * elementSize)),
                  length,
                  elementSize)
        { }

        #endregion

        #region IDisposable

        /// <summary>
        /// Frees the allocated unsafe memory.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            Marshal.FreeHGlobal(NativePtr);
            NativePtr = IntPtr.Zero;
        }

        #endregion
    }

    /// <summary>
    /// Represents a buffer that allocates native page-locked memory in the CPU
    /// address space using the <see cref="PageLockScope{T}"/> class.
    /// </summary>
    class PageLockedMemoryBuffer : UnmanagedMemoryBuffer
    {
        #region Instance

        [SuppressMessage(
            "Microsoft.Usage",
            "CA2213: Disposable fields should be disposed",
            Justification = "This is disposed in DisposeAcceleratorObject")]
        private readonly PageLockScope<byte> pageLockScope;

        /// <summary>
        /// Allocates an unmanaged page-locked memory buffer on the CPU.
        /// </summary>
        /// <param name="length">The length of this buffer.</param>
        /// <param name="elementSize">The element size.</param>
        internal PageLockedMemoryBuffer(long length, int elementSize)
            : base(length, elementSize)
        {
            pageLockScope = new CPUPageLockScope<byte>(NativePtr, LengthInBytes);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Frees the allocated pinned memory.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (disposing)
                pageLockScope.Dispose();
            base.DisposeAcceleratorObject(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Wraps a managed .Net array via a <see cref="GCHandle"/> instance.
    /// </summary>
    /// <remarks>
    /// Note that wrapped arrays will not have an associated target accelerator.
    /// </remarks>
    sealed class ArraySourceBuffer : CPUMemoryBuffer
    {
        #region Instance

        private readonly GCHandle handle;

        /// <summary>
        /// Constructs a new array wrapper.
        /// </summary>
        /// <param name="array">The managed array value.</param>
        /// <param name="elementSize">The element size.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySourceBuffer(Array array, int elementSize)
            : base(array.Length, elementSize)
        {
            handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            NativePtr = handle.AddrOfPinnedObject();
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Frees the internal GC handle.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing) =>
            handle.Free();

        #endregion
    }

    #endregion

    #region Static

    /// <summary>
    /// Creates a wrapped pointer memory buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value reference to the variable.</param>
    /// <param name="length">The length of this source.</param>
    /// <returns>A wrapped pointer memory buffer.</returns>
    public static unsafe CPUMemoryBuffer Create<T>(ref T value, long length)
        where T : unmanaged =>
        Create((T*)Unsafe.AsPointer(ref value), length);

    /// <summary>
    /// Creates a wrapped pointer memory buffer.
    /// </summary>
    /// <param name="value">The native value pointer.</param>
    /// <param name="length">The length of this source.</param>
    /// <returns>A wrapped pointer memory buffer.</returns>
    public static unsafe CPUMemoryBuffer Create<T>(T* value, long length)
        where T : unmanaged =>
        Create(new IntPtr(value), length, Interop.SizeOf<T>());

    /// <summary>
    /// Creates a wrapped pointer memory buffer.
    /// </summary>
    /// <param name="ptr">The native value pointer.</param>
    /// <param name="length">The length of this source.</param>
    /// <param name="elementSize">The element size.</param>
    /// <returns>A wrapped pointer memory buffer.</returns>
    public static unsafe CPUMemoryBuffer Create(
        IntPtr ptr,
        long length,
        int elementSize) =>
        new PointerSourceBuffer(ptr, length, elementSize);

    /// <summary>
    /// Creates a new unmanaged memory buffer.
    /// </summary>
    /// <param name="length">The length to allocate.</param>
    /// <param name="elementSize">The element size.</param>
    /// <returns>An unmanaged memory buffer.</returns>
    public static unsafe CPUMemoryBuffer Create(long length, int elementSize) =>
        new UnmanagedMemoryBuffer(length, elementSize);

    /// <summary>
    /// Creates a new page-locked unmanaged memory view source.
    /// </summary>
    /// <param name="length">The length to allocate.</param>
    /// <param name="elementSize">The element size.</param>
    /// <returns>An unsafe array view source.</returns>
    public static CPUMemoryBuffer CreateGenericPageLocked(long length, int elementSize) =>
        new PageLockedMemoryBuffer(length, elementSize);

    #endregion
}
