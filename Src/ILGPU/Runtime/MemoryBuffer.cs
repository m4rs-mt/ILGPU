// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents an abstract memory buffer that can be used in the scope of ILGPU
    /// runtime kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class MemoryBuffer : AcceleratorObject
    {
        #region Instance

        /// <summary>
        /// Initializes this buffer on the CPU.
        /// </summary>
        /// <param name="length">The length of this source.</param>
        /// <param name="elementSize">The element size.</param>
        protected MemoryBuffer(long length, int elementSize)
        {
            Init(length, elementSize);
        }

        /// <summary>
        /// Initializes this array view buffer.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="length">The length of this buffer.</param>
        /// <param name="elementSize">The element size.</param>
        protected MemoryBuffer(
            Accelerator accelerator,
            long length,
            int elementSize)
            : base(accelerator)
        {
            Init(length, elementSize);
        }

        /// <summary>
        /// Initializes the internal length properties.
        /// </summary>
        /// <param name="length">The length of this buffer.</param>
        /// <param name="elementSize">The element size.</param>
        private void Init(long length, int elementSize)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (elementSize < 1)
                throw new ArgumentOutOfRangeException(nameof(elementSize));

            Length = length;
            ElementSize = elementSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the native pointer of this buffer.
        /// </summary>
        public IntPtr NativePtr { get; protected set; }

        /// <summary>
        /// Returns the length of this buffer.
        /// </summary>
        public long Length { get; private set; }

        /// <summary>
        /// Returns the element size.
        /// </summary>
        public int ElementSize { get; private set; }

        /// <summary>
        /// Returns the length of this buffer in bytes.
        /// </summary>
        public long LengthInBytes => Length * ElementSize;

        #endregion

        #region Methods

        /// <summary>
        /// Sets the contents of the current buffer to the given byte value.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="value">The value to write into the memory buffer.</param>
        /// <param name="targetOffsetInBytes">The target offset in bytes.</param>
        /// <param name="length">The number of bytes to set.</param>
        public abstract void MemSet(
            AcceleratorStream stream,
            byte value,
            long targetOffsetInBytes,
            long length);

        /// <summary>
        /// Copies elements from the current buffer to the target view.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="sourceOffsetInBytes">The source offset in bytes.</param>
        /// <param name="targetView">The target view.</param>
        public abstract void CopyTo(
            AcceleratorStream stream,
            long sourceOffsetInBytes,
            in ArrayView<byte> targetView);

        /// <summary>
        /// Copies elements from the source view to the current buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetOffsetInBytes">The target offset in bytes.</param>
        public abstract void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            long targetOffsetInBytes);

        /// <summary>
        /// Returns a raw array view of the whole buffer.
        /// </summary>
        /// <returns>The raw array view.</returns>
        public ArrayView<byte> AsRawArrayView() =>
            AsRawArrayView(0L, LengthInBytes);

        /// <summary>
        /// Returns a raw array view starting at the given byte offset.
        /// </summary>
        /// <param name="offsetInBytes">The raw offset in bytes.</param>
        /// <returns>The raw array view.</returns>
        public ArrayView<byte> AsRawArrayView(long offsetInBytes) =>
            AsRawArrayView(offsetInBytes, LengthInBytes - offsetInBytes);

        /// <summary>
        /// Returns a raw array slice of the this buffer.
        /// </summary>
        /// <param name="offsetInBytes">The raw offset in bytes.</param>
        /// <param name="lengthInBytes">The raw length in bytes.</param>
        /// <returns></returns>
        public ArrayView<byte> AsRawArrayView(long offsetInBytes, long lengthInBytes)
        {
            if (offsetInBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(offsetInBytes));
            if ((LengthInBytes > 0) && (offsetInBytes >= LengthInBytes))
                throw new ArgumentOutOfRangeException(nameof(offsetInBytes));
            if (lengthInBytes < 0 || offsetInBytes + lengthInBytes > LengthInBytes)
                throw new ArgumentOutOfRangeException(nameof(lengthInBytes));
            return new ArrayView<byte>(this, offsetInBytes, lengthInBytes);
        }

        /// <summary>
        /// Gets an array view that spans the given number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The number of elements.</param>
        /// <returns>The created array view.</returns>
        public ArrayView<T> AsArrayView<T>(long offset, long length)
            where T : unmanaged
        {
            if (Interop.SizeOf<T>() != ElementSize)
            {
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NotSupportedType,
                    nameof(T)));
            }
            if ((Length > 0) && (offset >= Length))
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (offset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            return new ArrayView<T>(this, offset, length);
        }

        #endregion
    }

    /// <summary>
    /// An abstract memory buffer based on a specific view type.
    /// </summary>
    /// <typeparam name="TView">The underlying view type.</typeparam>
    public interface IMemoryBuffer<in TView> : IContiguousArrayView
        where TView : struct, IArrayView
    {
        /// <summary>
        /// Sets the contents of the current buffer to the given byte value.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="value">The value to write into the memory buffer.</param>
        /// <param name="targetOffsetInBytes">The target offset in bytes.</param>
        /// <param name="length">The number of bytes to set.</param>
        void MemSet(
            AcceleratorStream stream,
            byte value,
            long targetOffsetInBytes,
            long length);

        /// <summary>
        /// Copies elements from the current buffer to the target view.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="sourceOffsetInBytes">The source offset in bytes.</param>
        /// <param name="targetView">The target view.</param>
        void CopyTo(
            AcceleratorStream stream,
            long sourceOffsetInBytes,
            in ArrayView<byte> targetView);

        /// <summary>
        /// Copies elements from the source view to the current buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetOffsetInBytes">The target offset in bytes.</param>
        void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            long targetOffsetInBytes);
    }

    /// <summary>
    /// Represents an opaque memory buffer that can be used in the scope of ILGPU runtime
    /// kernels.
    /// </summary>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    [DebuggerDisplay("{View}")]
    public class MemoryBuffer<TView> : MemoryBuffer, IMemoryBuffer<TView>
        where TView : struct, IArrayView
    {
        #region Instance

        /// <summary>
        /// Initializes this memory buffer.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="view">The extent (number of elements).</param>
        protected internal MemoryBuffer(Accelerator accelerator, in TView view)
            : base(accelerator, view.Length, view.ElementSize)
        {
            View = view;
            NativePtr = Buffer.NativePtr;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the owned memory buffer instance.
        /// </summary>
        protected MemoryBuffer Buffer => View.Buffer;

        /// <summary>
        /// Returns the same memory buffer instance.
        /// </summary>
        MemoryBuffer IArrayView.Buffer => Buffer;

        /// <summary>
        /// Returns the base offset 0.
        /// </summary>
        [SuppressMessage(
            "Design",
            "CA1033:Interface methods should be callable by child types",
            Justification = "We do not want to expose this implementation here.")]
        long IContiguousArrayView.Index => 0L;

        /// <summary>
        /// Returns the base offset 0.
        /// </summary>
        [SuppressMessage(
            "Design",
            "CA1033:Interface methods should be callable by child types",
            Justification = "We do not want to expose this implementation here.")]
        long IContiguousArrayView.IndexInBytes => 0L;

        /// <summary>
        /// Returns an array view that can access this buffer.
        /// </summary>
        public TView View { get; private set; }

        /// <summary>
        /// Returns true if this buffer has not been disposed.
        /// </summary>
        public bool IsValid => !IsDisposed;

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void MemSet(
            AcceleratorStream stream,
            byte value,
            long targetOffsetInBytes,
            long length) =>
            Buffer.MemSet(
                stream,
                value,
                targetOffsetInBytes,
                length);

        /// <inheritdoc/>
        public override void CopyTo(
            AcceleratorStream stream,
            long sourceOffsetInBytes,
            in ArrayView<byte> targetView) =>
            Buffer.CopyTo(stream, sourceOffsetInBytes, targetView);

        /// <inheritdoc/>
        public override void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            long targetOffsetInBytes) =>
            Buffer.CopyFrom(stream, sourceView, targetOffsetInBytes);

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        /// <returns>An array view that can access this array.</returns>
        public TView ToArrayView() => View;

        #endregion

        #region IDisposable

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (!disposing)
                return;

            Buffer.Dispose();
            NativePtr = default;
            View = default;
        }

        #endregion

        #region Operators

        /// <summary>
        /// Implicitly converts this buffer into an array view.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        public static implicit operator TView(MemoryBuffer<TView> buffer) =>
            buffer.View;

        #endregion
    }
}
