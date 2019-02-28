// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: MemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents an abstract memory buffer that can be used in the scope
    /// of ILGPU runtime kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class MemoryBuffer : ArrayViewSource, IMemoryBuffer
    {
        #region Instance

        /// <summary>
        /// Initializes this memory buffer.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="length">The length in elements.</param>
        protected MemoryBuffer(Accelerator accelerator, int length)
            : base(accelerator)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(length));
            Length = length;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the length of this buffer.
        /// </summary>
        public int Length { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the contents of the given buffer to zero using
        /// the default accelerator stream.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MemSetToZero() =>
            MemSetToZero(Accelerator.DefaultStream);

        /// <summary>
        /// Sets the contents of the current buffer to zero.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        public abstract void MemSetToZero(AcceleratorStream stream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <returns>A new array holding the requested contents.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetAsRawArray() => GetAsRawArray(Accelerator.DefaultStream);

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public abstract byte[] GetAsRawArray(AcceleratorStream stream);

        #endregion
    }

    /// <summary>
    /// Represents an abstract memory buffer that can be used in the scope
    /// of ILGPU runtime kernels.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class MemoryBuffer<T, TIndex> : MemoryBuffer, IMemoryBuffer<T, TIndex>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        #region Constants

        /// <summary>
        /// Represents the size of an element in bytes.
        /// </summary>
        public static readonly int ElementSize = ArrayView<T, TIndex>.ElementSize;

        #endregion

        #region Instance

        /// <summary>
        /// Initializes this memory buffer.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="extent">The extent (number of elements).</param>
        protected unsafe MemoryBuffer(
            Accelerator accelerator,
            TIndex extent)
            : base(accelerator, extent.Size)
        {
            Extent = extent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the length of this buffer in bytes.
        /// </summary>
        public Index LengthInBytes => new Index(Length) * ElementSize;

        /// <summary>
        /// Returns an array view that can access this buffer.
        /// </summary>
        public ArrayView<T, TIndex> View => new ArrayView<T, TIndex>(
            new ArrayView<T>(this, 0, Extent.Size), Extent);

        /// <summary>
        /// Returns the extent of this buffer.
        /// </summary>
        public TIndex Extent { get; }

        #endregion

        #region ArrayViewBuffer

        /// <summary>
        /// Computes the effective address for the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The computed pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void* ComputeEffectiveAddress(Index index)
        {
            ref var address = ref Interop.ComputeEffectiveAddress(
                ref Unsafe.AsRef<byte>(NativePtr.ToPointer()),
                index,
                ElementSize);
            return Unsafe.AsPointer(ref address);
        }

        #endregion

        #region View Methods

        /// <summary>
        /// Copies elements from the current buffer to the target view.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        protected internal abstract void CopyToView(
            AcceleratorStream stream,
            ArrayView<T> target,
            Index sourceOffset);

        /// <summary>
        /// Copies elements from the source view to the current buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        protected internal abstract void CopyFromView(
            AcceleratorStream stream,
            ArrayView<T> source,
            Index targetOffset);

        /// <summary>
        /// Copies elements from the current buffer to the target view using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(ArrayView<T, TIndex> target, TIndex sourceOffset) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        public void CopyTo(
            AcceleratorStream stream,
            ArrayView<T, TIndex> target,
            TIndex sourceOffset)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!target.IsValid)
                throw new ArgumentNullException(nameof(target));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (!sourceOffset.Add(target.Extent).InBoundsInclusive(Extent))
                throw new ArgumentOutOfRangeException(nameof(target));

            CopyToView(
                stream,
                target.AsLinearView(),
                sourceOffset.ComputeLinearIndex(Extent));
        }

        /// <summary>
        /// Copies elements to the current buffer from the source view.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(ArrayView<T, TIndex> source, TIndex targetOffset) =>
            CopyFrom(
                Accelerator.DefaultStream,
                source,
                targetOffset);

        /// <summary>
        /// Copies elements to the current buffer from the source view.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        public void CopyFrom(
            AcceleratorStream stream,
            ArrayView<T, TIndex> source,
            TIndex targetOffset)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!source.IsValid)
                throw new ArgumentNullException(nameof(source));
            if (!targetOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (!targetOffset.Add(source.Extent).InBoundsInclusive(Extent))
                throw new ArgumentOutOfRangeException(nameof(source));

            CopyFromView(
                stream,
                source.AsLinearView(),
                targetOffset.ComputeLinearIndex(Extent));
        }

        #endregion

        #region Copy Methods

        /// <summary>
        /// Copies elements from the current buffer to the target buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(MemoryBuffer<T, TIndex> target, TIndex sourceOffset) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            AcceleratorStream stream,
            MemoryBuffer<T, TIndex> target,
            TIndex sourceOffset) =>
            CopyTo(
                stream,
                target,
                sourceOffset,
                default,
                Extent);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            MemoryBuffer<T, TIndex> target,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyTo(
            AcceleratorStream stream,
            MemoryBuffer<T, TIndex> target,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (!targetOffset.InBounds(extent))
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (!sourceOffset.Add(extent).InBoundsInclusive(Extent) ||
                !targetOffset.Add(extent).InBoundsInclusive(target.Extent))
                throw new ArgumentOutOfRangeException(nameof(extent));

            CopyToView(
                stream,
                target.GetSubView(targetOffset, extent).AsLinearView(),
                sourceOffset.ComputeLinearIndex(Extent));
        }

        /// <summary>
        /// Copies a single element of this buffer to the given target variable
        /// in CPU memory using the default accelerator stream.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="targetIndex">The target index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(out T target, TIndex targetIndex) =>
            CopyTo(Accelerator.DefaultStream, out target, targetIndex);

        /// <summary>
        /// Copies a single element of this buffer to the given target variable
        /// in CPU memory.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target location.</param>
        /// <param name="targetIndex">The target index.</param>
        public void CopyTo(
            AcceleratorStream stream,
            out T target,
            TIndex targetIndex)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            target = default;
            using (var wrapper = ViewPointerWrapper.Create(ref target))
            {
                CopyToView(
                    stream,
                    new ArrayView<T>(wrapper, 0, 1),
                    targetIndex.ComputeLinearIndex(Extent));
            }
            stream.Synchronize();
        }

        /// <summary>
        /// Copies the contents of this buffer into the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            T[] target,
            TIndex sourceOffset,
            int targetOffset,
            TIndex extent) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies the contents of this buffer into the given array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyTo(
            AcceleratorStream stream,
            T[] target,
            TIndex sourceOffset,
            int targetOffset,
            TIndex extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            var length = target.Length;
            if (targetOffset >= length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (extent.Size < 1 || !sourceOffset.Add(extent).InBoundsInclusive(Extent))
                throw new ArgumentOutOfRangeException(nameof(extent));

            Debug.Assert(target.Rank == 1);

            var handle = GCHandle.Alloc(target, GCHandleType.Pinned);
            try
            {
                using (var wrapper = ViewArrayWrapper.Create<T>(handle))
                {
                    CopyToView(
                        stream,
                        new ArrayView<T>(wrapper, 0, length).GetSubView(
                            targetOffset, extent.Size),
                        sourceOffset.ComputeLinearIndex(Extent));
                }
                stream.Synchronize();
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Copies elements to the current buffer from the source buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="targetOffset">The target offset.</param>
        public void CopyFrom(MemoryBuffer<T, TIndex> source, TIndex targetOffset) =>
            CopyFrom(Accelerator.DefaultStream, source, targetOffset);

        /// <summary>
        /// Copies elements to the current buffer from the source buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(
            MemoryBuffer<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent) =>
            CopyFrom(
                Accelerator.DefaultStream,
                source,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(
            AcceleratorStream stream,
            MemoryBuffer<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            source.CopyTo(
                stream,
                this,
                targetOffset,
                sourceOffset,
                extent);
        }

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="targetOffset">The target offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(
            AcceleratorStream stream,
            MemoryBuffer<T, TIndex> source,
            TIndex targetOffset) =>
            CopyFrom(
                stream,
                source,
                default,
                targetOffset,
                source.Extent);

        /// <summary>
        /// Copies a single element from CPU memory to this buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="sourceIndex">The source index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(T source, TIndex sourceIndex) =>
            CopyFrom(Accelerator.DefaultStream, source, sourceIndex);

        /// <summary>
        /// Copies a single element from CPU memory to this buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source value.</param>
        /// <param name="sourceIndex">The source index.</param>
        public void CopyFrom(
            AcceleratorStream stream,
            T source,
            TIndex sourceIndex)
        {
            using (var wrapper = ViewPointerWrapper.Create(ref source))
            {
                CopyFromView(
                    stream,
                    new ArrayView<T>(wrapper, 0, 1),
                    sourceIndex.ComputeLinearIndex(Extent));
                stream.Synchronize();
            }
        }

        /// <summary>
        /// Copies the contents to this buffer from the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(
            T[] source,
            int sourceOffset,
            TIndex targetOffset,
            int extent) =>
            CopyFrom(
                Accelerator.DefaultStream,
                source,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies the contents to this buffer from the given array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyFrom(
            AcceleratorStream stream,
            T[] source,
            int sourceOffset,
            TIndex targetOffset,
            int extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var length = source.Length;
            if (sourceOffset >= length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (!targetOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (extent < 1 || extent > source.Length)
                throw new ArgumentOutOfRangeException(nameof(extent));
            if (sourceOffset + extent < 1 || extent + sourceOffset > source.Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));

            GCHandle handle = GCHandle.Alloc(source, GCHandleType.Pinned);
            try
            {
                using (var wrapper = ViewArrayWrapper.Create<T>(handle))
                {
                    CopyFromView(
                        stream,
                        new ArrayView<T>(wrapper, 0, source.Length).GetSubView(
                            sourceOffset, extent),
                        targetOffset.ComputeLinearIndex(Extent));
                    stream.Synchronize();
                }
            }
            finally
            {
                handle.Free();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Copies the current contents into a new array using
        /// the default accelerator stream.
        /// </summary>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray() => GetAsArray(Accelerator.DefaultStream);

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray(AcceleratorStream stream) =>
            GetAsArray(stream, default, Extent);

        /// <summary>
        /// Copies the current contents into a new array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray(TIndex offset, TIndex extent) =>
            GetAsArray(Accelerator.DefaultStream, offset, extent);

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray(AcceleratorStream stream, TIndex offset, TIndex extent)
        {
            var length = extent.Size;
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(extent));

            var result = new T[length];
            CopyTo(stream, result, offset, 0, extent);
            return result;
        }

        /// <summary cref="ArrayViewSource.GetAsRawArray(AcceleratorStream, Index, Index)"/>
        protected internal sealed override unsafe ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            Index byteOffset,
            Index byteExtent)
        {
            var rawOffset = byteOffset - byteOffset % ElementSize;
            int offset = byteExtent + rawOffset;
            var rawExtent = ABI.Align(offset, ElementSize);

            var result = new byte[rawExtent];
            fixed (byte *ptr = &result[0])
            {
                using (var wrapper = ViewPointerWrapper.Create(new IntPtr(ptr)))
                {
                    CopyToView(
                        stream,
                        new ArrayView<T>(
                            wrapper,
                            0,
                            rawExtent / ElementSize),
                        rawOffset / ElementSize);
                }
            }
            return new ArraySegment<byte>(
                result,
                rawOffset,
                rawExtent);
        }

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public override byte[] GetAsRawArray(AcceleratorStream stream) =>
            GetAsRawArray(stream, Index.Zero, LengthInBytes).Array;

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T, TIndex> GetSubView(TIndex offset) =>
            View.GetSubView(offset);

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <param name="subViewExtent">The extent of the new subview.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T, TIndex> GetSubView(TIndex offset, TIndex subViewExtent) =>
            View.GetSubView(offset, subViewExtent);

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        /// <returns>An array view that can access this array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T, TIndex> ToArrayView() => View;

        #endregion

        #region Operators

        /// <summary>
        /// Implicitly converts this buffer into an array view.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ArrayView<T, TIndex>(MemoryBuffer<T, TIndex> buffer)
        {
            Debug.Assert(buffer != null, "Invalid buffer");
            return buffer.View;
        }

        #endregion
    }
}
