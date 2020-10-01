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

using ILGPU.IR.Types;
using System;
using System.Runtime.CompilerServices;

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
        protected MemoryBuffer(Accelerator accelerator, long length)
            : base(accelerator)
        {
            if (length < 1L)
                throw new ArgumentOutOfRangeException(nameof(length));
            Length = length;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the length of this buffer.
        /// </summary>
        public long Length { get; }

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
    public abstract class MemoryBuffer<T, TIndex> :
        MemoryBuffer, IMemoryBuffer<T, TIndex>
        where T : unmanaged
        where TIndex : unmanaged, IGenericIndex<TIndex>
    {
        #region Constants

        /// <summary>
        /// Represents the size of an element in bytes.
        /// </summary>
        public static int ElementSize => ArrayView<T, TIndex>.ElementSize;

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
        public long LengthInBytes => Length * ElementSize;

        /// <summary>
        /// Returns an array view that can access this buffer.
        /// </summary>
        public ArrayView<T, TIndex> View => new ArrayView<T, TIndex>(
            new ArrayView<T>(this, 0L, Extent.Size), Extent);

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
        internal unsafe void* ComputeEffectiveAddress(LongIndex1 index)
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
            LongIndex1 sourceOffset);

        /// <summary>
        /// Copies elements from the source view to the current buffer.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        protected internal abstract void CopyFromView(
            AcceleratorStream stream,
            ArrayView<T> source,
            LongIndex1 targetOffset);

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
                sourceOffset.ComputeLongLinearIndex(Extent));
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
                targetOffset.ComputeLongLinearIndex(Extent));
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
                Length);

        /// <summary>
        /// Copies elements from the current buffer to the target buffer using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use CopyTo(MemoryBuffer<T, TIndex>, TIndex, TIndex, Index1) instead")]
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
        [Obsolete("Use CopyTo(AcceleratorStream, MemoryBuffer<T, TIndex>, TIndex, " +
            "TIndex, Index1) instead")]
        public void CopyTo(
            AcceleratorStream stream,
            MemoryBuffer<T, TIndex> target,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent) =>
            CopyTo(
                stream,
                target,
                sourceOffset,
                targetOffset,
                extent.Size);

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
            LongIndex1 extent) =>
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
            LongIndex1 extent)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (!targetOffset.InBounds(target.Extent))
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            var linearSourceIndex = sourceOffset.ComputeLongLinearIndex(Extent);
            var linearTargetIndex = targetOffset.ComputeLongLinearIndex(target.Extent);
            if (linearSourceIndex + extent > Length ||
                linearTargetIndex + extent > target.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            CopyToView(
                stream,
                target.View.GetSubView(targetOffset, extent),
                linearSourceIndex);
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
                    targetIndex.ComputeLongLinearIndex(Extent));
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
            Span<T> target,
            TIndex sourceOffset,
            long targetOffset,
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
        public unsafe void CopyTo(
            AcceleratorStream stream,
            Span<T> target,
            TIndex sourceOffset,
            long targetOffset,
            TIndex extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            var length = target.Length;
            if (targetOffset < 0 || targetOffset >= length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (extent.Size < 1 ||
                targetOffset + extent.Size > length ||
                !sourceOffset.Add(extent).InBoundsInclusive(Extent))
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            fixed (T* ptr = &target[0])
            {
                using (var wrapper = ViewPointerWrapper.Create(ptr))
                {
                    CopyToView(
                        stream,
                        new ArrayView<T>(wrapper, 0, length).GetSubView(
                            targetOffset, extent.Size),
                        sourceOffset.ComputeLongLinearIndex(Extent));
                }
                stream.Synchronize();
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
        [Obsolete("Use CopyFrom(MemoryBuffer<T, TIndex>, TIndex, TIndex, Index) " +
            "instead")]
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
        [Obsolete("Use CopyFrom(AcceleratorStream, MemoryBuffer<T, TIndex>, TIndex, " +
            "TIndex, Index) instead")]
        public void CopyFrom(
            AcceleratorStream stream,
            MemoryBuffer<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent) =>
            CopyFrom(
                stream,
                source,
                sourceOffset,
                targetOffset,
                extent.Size);

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
            LongIndex1 extent) =>
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
            LongIndex1 extent)
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
                source.Length);

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
            using var wrapper = ViewPointerWrapper.Create(ref source);
            CopyFromView(
                stream,
                new ArrayView<T>(wrapper, 0, 1),
                sourceIndex.ComputeLongLinearIndex(Extent));
            stream.Synchronize();
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
            ReadOnlySpan<T> source,
            long sourceOffset,
            TIndex targetOffset,
            long extent) =>
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
        public unsafe void CopyFrom(
            AcceleratorStream stream,
            ReadOnlySpan<T> source,
            long sourceOffset,
            TIndex targetOffset,
            long extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var length = source.Length;
            if (sourceOffset < 0 || sourceOffset >= length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            var linearIndex = targetOffset.ComputeLongLinearIndex(Extent);
            if (!targetOffset.InBounds(Extent) || linearIndex >= Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (extent < 1 || extent > source.Length ||
                extent + sourceOffset > source.Length ||
                linearIndex + extent > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            fixed (T* ptr = &source[0])
            {
                using var wrapper = ViewPointerWrapper.Create(ptr);
                CopyFromView(
                    stream,
                    new ArrayView<T>(wrapper, 0, source.Length).GetSubView(
                        sourceOffset,
                        extent),
                    linearIndex);
                stream.Synchronize();
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

        /// <summary cref="ArrayViewSource.GetAsRawArray(
        /// AcceleratorStream, long, long)"/>
        protected internal sealed override unsafe ArraySegment<byte> GetAsRawArray(
            AcceleratorStream stream,
            long byteOffset,
            long byteExtent)
        {
            var rawOffset = byteOffset - byteOffset % ElementSize;
            var offset = byteExtent + rawOffset;
            var rawExtent = TypeNode.Align(offset, ElementSize);

            var result = new byte[rawExtent];
            fixed (byte* ptr = &result[0])
            {
                using var wrapper = ViewPointerWrapper.Create(ptr);
                CopyToView(
                    stream,
                    new ArrayView<T>(wrapper, 0, rawExtent / ElementSize),
                    rawOffset / ElementSize);
            }

            IndexTypeExtensions.AssertIntIndexRange(rawOffset);
            IndexTypeExtensions.AssertIntIndexRange(rawExtent);
            return new ArraySegment<byte>(
                result,
                (int)rawOffset,
                (int)rawExtent);
        }

        /// <summary>
        /// Copies the current contents into a new byte array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        public override byte[] GetAsRawArray(AcceleratorStream stream) =>
            GetAsRawArray(stream, LongIndex1.Zero, LengthInBytes).Array;

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
        public static implicit operator ArrayView<T, TIndex>(
            MemoryBuffer<T, TIndex> buffer) =>
            buffer.View;

        #endregion
    }
}
