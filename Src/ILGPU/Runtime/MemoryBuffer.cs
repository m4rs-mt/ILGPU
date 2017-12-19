// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: MemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

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
    public abstract class MemoryBuffer : AcceleratorObject, IMemoryBuffer
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
        /// Returns the native pointer.
        /// </summary>
        public IntPtr Pointer { get; protected set; }

        /// <summary>
        /// Returns the length of this buffer.
        /// </summary>
        public int Length { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        public abstract Array GetAsRawArray(
            int offset,
            int extent);


        /// <summary>
        /// Sets the contents of the current buffer to zero.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        public abstract void MemSetToZero(AcceleratorStream stream);

        /// <summary>
        /// Sets the contents of the current buffer to zero.
        /// </summary>
        public void MemSetToZero()
        {
            MemSetToZero(Accelerator.DefaultStream);
        }

        #endregion
    }

    /// <summary>
    /// Represents an abstract memory buffer that can be used in the scope
    /// of ILGPU runtime kernels.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class MemoryBuffer<T, TIndex> : MemoryBuffer
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
        protected MemoryBuffer(Accelerator accelerator, TIndex extent)
            : base(accelerator, extent.Size)
        {
            Extent = extent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the length of this buffer in bytes.
        /// </summary>
        public int LengthInBytes => Length * ElementSize;

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        public ArrayView<T, TIndex> View => new ArrayView<T, TIndex>(Pointer, Extent);

        /// <summary>
        /// Returns the extent of this buffer.
        /// </summary>
        public TIndex Extent { get; }

        /// <summary>
        /// Accesses this memory buffer from the CPU.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        /// <remarks>
        /// Note that this operation involves a synchronous memory copy.
        /// Do not use this operation frequently or in high-performance scenarios.
        /// </remarks>
        public T this[TIndex index]
        {
            get
            {
                CopyTo(out T variable, index);
                return variable;
            }
            set
            {
                CopyFrom(value, index);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Copies elements from the current buffer to the target view.
        /// </summary>
        /// <param name="target">The target view.</param>
        /// <param name="acceleratorType">The accelerator type of the view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="stream">The used accelerator stream.</param>
        protected internal abstract void CopyToViewInternal(
            ArrayView<T, Index> target,
            AcceleratorType acceleratorType,
            TIndex sourceOffset,
            AcceleratorStream stream);

        /// <summary>
        /// Copies elements from the source view to the current buffer.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <param name="acceleratorType">The accelerator type of the view.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="stream">The used accelerator stream.</param>
        protected internal abstract void CopyFromViewInternal(
            ArrayView<T, Index> source,
            AcceleratorType acceleratorType,
            TIndex targetOffset,
            AcceleratorStream stream);

        #endregion

        #region CopyTo Methods

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <param name="stream">The used accelerator stream.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            MemoryBuffer<T, TIndex> target,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent,
            AcceleratorStream stream)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (!targetOffset.InBounds(target.Extent))
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (!sourceOffset.Add(extent).InBoundsInclusive(Extent) ||
                !targetOffset.Add(extent).InBoundsInclusive(target.Extent))
                throw new ArgumentOutOfRangeException(nameof(extent));

            CopyToViewInternal(
                target.GetSubView(targetOffset, extent).AsLinearView(),
                target.Accelerator.AcceleratorType,
                sourceOffset,
                stream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyTo(
            MemoryBuffer<T, TIndex> target,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent)
        {
            CopyTo(target, sourceOffset, targetOffset, extent, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        public void CopyTo(
            MemoryBuffer<T, TIndex> target,
            TIndex sourceOffset)
        {
            CopyTo(target, sourceOffset, default(TIndex), Extent, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="target">The target buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="stream">The used accelerator stream.</param>
        public void CopyTo(
            MemoryBuffer<T, TIndex> target,
            TIndex sourceOffset,
            AcceleratorStream stream)
        {
            CopyTo(target, sourceOffset, new TIndex(), Extent, stream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="target">The target view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="stream">The used accelerator stream.</param>
        public void CopyToView(
            ArrayView<T, TIndex> target,
            TIndex sourceOffset,
            AcceleratorStream stream)
        {
            var targetType = Accelerator.TryResolvePointerType(target.Pointer);
            CopyToView(
                target,
                targetType ?? Accelerator.AcceleratorType,
                sourceOffset,
                stream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="target">The target view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        public void CopyToView(
            ArrayView<T, TIndex> target,
            TIndex sourceOffset)
        {
            CopyToView(target, sourceOffset, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="target">The target view.</param>
        /// <param name="acceleratorType">The accelerator type of the view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        public void CopyToView(
            ArrayView<T, TIndex> target,
            AcceleratorType acceleratorType,
            TIndex sourceOffset)
        {
            CopyToView(target, acceleratorType, sourceOffset, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="target">The target view.</param>
        /// <param name="acceleratorType">The accelerator type of the view.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="stream">The used accelerator stream.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyToView(
            ArrayView<T, TIndex> target,
            AcceleratorType acceleratorType,
            TIndex sourceOffset,
            AcceleratorStream stream)
        {
            if (!target.IsValid)
                throw new ArgumentNullException(nameof(target));
            if (!sourceOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            if (!sourceOffset.Add(target.Extent).InBoundsInclusive(Extent))
                throw new ArgumentOutOfRangeException(nameof(target));

            CopyToViewInternal(target.AsLinearView(), acceleratorType, sourceOffset, stream);
        }

        /// <summary>
        /// Copies a single element of this buffer to the given target variable
        /// in CPU memory.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="targetIndex">The target index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(out T target, TIndex targetIndex)
        {
            CopyTo(out target, targetIndex, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies a single element of this buffer to the given target variable
        /// in CPU memory.
        /// </summary>
        /// <param name="target">The target location.</param>
        /// <param name="targetIndex">The target index.</param>
        /// <param name="stream">The used accelerator stream.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(
            out T target,
            TIndex targetIndex,
            AcceleratorStream stream)
        {
            target = default(T);
            var ptr = Interop.GetAddress(ref target);
            CopyToViewInternal(
                new ArrayView<T, Index>(ptr, 1),
                AcceleratorType.CPU,
                targetIndex,
                stream);
        }

        /// <summary>
        /// Copies the contents of this buffer into the given array.
        /// </summary>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyTo(
            T[] target,
            TIndex sourceOffset,
            int targetOffset,
            TIndex extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
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
                CopyToViewInternal(
                    new ArrayView<T>(handle.AddrOfPinnedObject(), length).GetSubView(
                        targetOffset, extent.Size),
                    AcceleratorType.CPU,
                    sourceOffset,
                    Accelerator.DefaultStream);
            }
            finally
            {
                handle.Free();
            }
        }

        #endregion

        #region CopyFrom Methods

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <param name="stream">The used accelerator stream.</param>
        public void CopyFrom(
            MemoryBuffer<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent,
            AcceleratorStream stream)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            source.CopyTo(this, targetOffset, sourceOffset, extent, stream);
        }

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyFrom(
            MemoryBuffer<T, TIndex> source,
            TIndex sourceOffset,
            TIndex targetOffset,
            TIndex extent)
        {
            CopyFrom(source, sourceOffset, targetOffset, extent, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="targetOffset">The target offset.</param>
        public void CopyFrom(
            MemoryBuffer<T, TIndex> source,
            TIndex targetOffset)
        {
            CopyFrom(source, default(TIndex), targetOffset, source.Extent, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies elements to the current buffer from the source buffer.
        /// </summary>
        /// <param name="source">The source buffer.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="stream">The used accelerator stream.</param>
        public void CopyFrom(
            MemoryBuffer<T, TIndex> source,
            TIndex targetOffset,
            AcceleratorStream stream)
        {
            CopyFrom(source, default(TIndex), targetOffset, source.Extent, stream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="stream">The used accelerator stream.</param>
        public void CopyFromView(
            ArrayView<T, TIndex> source,
            TIndex targetOffset,
            AcceleratorStream stream)
        {
            var sourceType = Accelerator.TryResolvePointerType(source.Pointer);
            CopyFromView(
                source,
                sourceType ?? Accelerator.AcceleratorType,
                targetOffset,
                stream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <param name="targetOffset">The target offset.</param>
        public void CopyFromView(
            ArrayView<T, TIndex> source,
            TIndex targetOffset)
        {
            CopyFromView(source, targetOffset, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <param name="acceleratorType">The accelerator type of the view.</param>
        /// <param name="targetOffset">The target offset.</param>
        public void CopyFromView(
            ArrayView<T, TIndex> source,
            AcceleratorType acceleratorType,
            TIndex targetOffset)
        {
            CopyFromView(source, acceleratorType, targetOffset, Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies elements from the current buffer to the target buffer.
        /// </summary>
        /// <param name="source">The source view.</param>
        /// <param name="acceleratorType">The accelerator type of the view.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="stream">The used accelerator stream.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFromView(
            ArrayView<T, TIndex> source,
            AcceleratorType acceleratorType,
            TIndex targetOffset,
            AcceleratorStream stream)
        {
            if (!source.IsValid)
                throw new ArgumentNullException(nameof(source));
            if (!targetOffset.InBounds(Extent))
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (!targetOffset.Add(source.Extent).InBoundsInclusive(Extent))
                throw new ArgumentOutOfRangeException(nameof(source));

            CopyFromViewInternal(source.AsLinearView(), acceleratorType, targetOffset, stream);
        }

        /// <summary>
        /// Copies a single element from CPU memory to this buffer.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="sourceIndex">The source index.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(T source, TIndex sourceIndex)
        {
            var ptr = Interop.GetAddress(ref source);
            CopyFromViewInternal(
                new ArrayView<T>(ptr, 1),
                AcceleratorType.CPU,
                sourceIndex,
                Accelerator.DefaultStream);
        }

        /// <summary>
        /// Copies the contents to this buffer from the given array.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public void CopyFrom(
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
                CopyFromViewInternal(
                    new ArrayView<T>(handle.AddrOfPinnedObject(), extent).GetSubView(
                        sourceOffset, extent),
                    AcceleratorType.CPU,
                    targetOffset,
                    Accelerator.DefaultStream);
            }
            finally
            {
                handle.Free();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray()
        {
            return GetAsArray(new TIndex(), Extent);
        }

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        public T[] GetAsArray(TIndex offset, TIndex extent)
        {
            var length = extent.Size;
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(extent));

            var result = new T[length];
            CopyTo(result, offset, 0, extent);
            return result;
        }

        /// <summary>
        /// Copies the current contents into a new array.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        public override Array GetAsRawArray(int offset, int extent)
        {
            var sourceOffset = Extent.ReconstructIndex(offset);
            var sourceLength = Extent.ReconstructIndex(extent);
            return GetAsArray(sourceOffset, sourceLength);
        }

        /// <summary>
        /// Returns a variable view for the element at the given index.
        /// </summary>
        /// <param name="index">The target index.</param>
        /// <returns>A variable view for the element at the given index.</returns>
        public VariableView<T> GetVariableView(TIndex index)
        {
            return View.GetVariableView(index);
        }

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <returns>The new subview.</returns>
        public ArrayView<T, TIndex> GetSubView(TIndex offset)
        {
            return View.GetSubView(offset);
        }

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <param name="subViewExtent">The extent of the new subview.</param>
        /// <returns>The new subview.</returns>
        public ArrayView<T, TIndex> GetSubView(TIndex offset, TIndex subViewExtent)
        {
            return View.GetSubView(offset, subViewExtent);
        }

        /// <summary>
        /// Returns an array view that can access this array.
        /// </summary>
        /// <returns>An array view that can access this array.</returns>
        public ArrayView<T, TIndex> ToArrayView()
        {
            return View;
        }

        #endregion

        #region Operators

        /// <summary>
        /// Implicitly converts this buffer into an array view.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        public static implicit operator ArrayView<T, TIndex>(MemoryBuffer<T, TIndex> buffer)
        {
            Debug.Assert(buffer != null, "Invalid buffer");
            return buffer.View;
        }

        #endregion
    }
}
