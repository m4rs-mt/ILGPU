// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: ArrayView.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Represents a generic view to an n-dimensional array on an accelerator.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    [DebuggerTypeProxy(typeof(DebugArrayView<,>))]
    [DebuggerDisplay("Extent = {Extent}, Length = {Length}, Ptr = {Pointer}")]
    public struct ArrayView<T, TIndex> : IEquatable<ArrayView<T, TIndex>>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        #region Constants

        /// <summary>
        /// Represents the native size of a single element.
        /// </summary>
        public static readonly int ElementSize = Interop.SizeOf<T>();

        #endregion

        #region Instance

        private IntPtr ptr;
        private TIndex extent;

        /// <summary>
        /// Constructs a new array view.
        /// </summary>
        /// <param name="data">The data pointer to the first element.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public ArrayView(IntPtr data, TIndex extent)
        {
            Debug.Assert(extent.Size > 0, "Extent of of range");
            ptr = data;
            this.extent = extent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true iff this view points to a valid location.
        /// </summary>
        public bool IsValid => ptr != IntPtr.Zero;

        /// <summary>
        /// Returns the address of the first element.
        /// </summary>
        public IntPtr Pointer => ptr;

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        public int Length => extent.Size;

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public TIndex Extent => extent;

        /// <summary>
        /// Returns the length of this array view in bytes.
        /// </summary>
        public int LengthInBytes => Length * ElementSize;

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        [SuppressMessage("Microsoft.Design", "CA1043:UseIntegralOrStringArgumentForIndexers")]
        public T this[TIndex index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Load(index); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { Store(index, value); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads a reference to the element at the given index as ref T.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The loaded reference to the desired element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T LoadRef(TIndex index)
        {
            Debug.Assert(index.InBounds(extent), "Index ouf of bounds");
            var elementIndex = index.ComputeLinearIndex(extent);
            Debug.Assert(elementIndex >= 0 && elementIndex < Length, "Index ouf of bounds");
            var elementPtr = Interop.LoadEffectiveAddress(ptr, ElementSize, elementIndex);
            return ref Interop.GetRef<T>(elementPtr);
        }

        /// <summary>
        /// Loads the element at the given index as T.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The loaded element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Load(TIndex index)
        {
            Debug.Assert(index.InBounds(extent), "Index ouf of bounds");
            var elementIndex = index.ComputeLinearIndex(extent);
            Debug.Assert(elementIndex >= 0 && elementIndex < Length, "Index ouf of bounds");
            return Interop.PtrToStructure<T>(ptr, ElementSize, elementIndex);
        }

        /// <summary>
        /// Stores the value into the element at the given index as T.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <param name="value">The value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Store(TIndex index, T value)
        {
            Debug.Assert(index.InBounds(extent), "Index ouf of bounds");
            var elementIndex = index.ComputeLinearIndex(extent);
            Debug.Assert(elementIndex >= 0 && elementIndex < Length, "Index ouf of bounds");
            Interop.StructureToPtr(value, ptr, ElementSize, elementIndex);
        }

        /// <summary>
        /// Returns a variable view that targets the element at the given index.
        /// </summary>
        /// <param name="index">The target index.</param>
        /// <returns>A variable view that targets the element at the given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VariableView<T> GetVariableView(TIndex index)
        {
            Debug.Assert(index.InBounds(extent), "Index ouf of bounds");
            var elementIndex = index.ComputeLinearIndex(extent);
            Debug.Assert(elementIndex >= 0 && elementIndex < Length, "Index ouf of bounds");
            return new VariableView<T>(
                Interop.LoadEffectiveAddress(ptr, ElementSize, elementIndex));
        }

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T, TIndex> GetSubView(TIndex offset)
        {
            Debug.Assert(offset.InBounds(extent), "Offset ouf of bounds");
            return GetSubView(offset, extent.Subtract(offset));
        }

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="offset">The starting offset.</param>
        /// <param name="subViewExtent">The extent of the new subview.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T, TIndex> GetSubView(TIndex offset, TIndex subViewExtent)
        {
            Debug.Assert(offset.InBounds(extent), "Offset ouf of bounds");
            var elementIndex = offset.ComputeLinearIndex(extent);
            Debug.Assert(elementIndex >= 0 && elementIndex < Length, "Offset ouf of bounds");
            Debug.Assert(offset.Add(subViewExtent).InBoundsInclusive(extent), "Subview out of range");
            return new ArrayView<T, TIndex>(
                Interop.LoadEffectiveAddress(ptr, ElementSize, elementIndex),
                subViewExtent);
        }

        /// <summary>
        /// Casts the current array view into another array-view type.
        /// </summary>
        /// <typeparam name="TOther">The target type.</typeparam>
        /// <returns>The casted array view.</returns>
        public ArrayView<TOther, TIndex> Cast<TOther>()
            where TOther : struct
        {
            var newExtent = Extent.ComputedCastedExtent(extent, ElementSize, ArrayView<TOther, TIndex>.ElementSize);
            return Cast<TOther, TIndex>(newExtent);
        }

        /// <summary>
        /// Casts the current array view into another array-view type.
        /// </summary>
        /// <typeparam name="TOther">The target type.</typeparam>
        /// <typeparam name="TOtherIndex">The target index type.</typeparam>
        /// <returns>The casted array view.</returns>
        public ArrayView<TOther, TOtherIndex> Cast<TOther, TOtherIndex>(TOtherIndex targetExtent)
            where TOther : struct
            where TOtherIndex : struct, IIndex, IGenericIndex<TOtherIndex>
        {
            Debug.Assert(targetExtent.Size > 0, "OutOfBounds cast");
            Debug.Assert(targetExtent.Size * ArrayView<TOther, TOtherIndex>.ElementSize <= Extent.Size * ElementSize, "OutOfBounds cast");
            return new ArrayView<TOther, TOtherIndex>(ptr, targetExtent);
        }

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        public ArrayView<T, Index> AsLinearView()
        {
            return new ArrayView<T, Index>(ptr, Length);
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given view is equal to the current view.
        /// </summary>
        /// <param name="other">The other view.</param>
        /// <returns>True, iff the given view is equal to the current view.</returns>
        public bool Equals(ArrayView<T, TIndex> other)
        {
            return other == this;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current view.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current view.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ArrayView<T, TIndex>)
                return Equals((ArrayView<T, TIndex>)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this view.
        /// </summary>
        /// <returns>The hash code of this view.</returns>
        public override int GetHashCode()
        {
            return ptr.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this view.
        /// </summary>
        /// <returns>The string representation of this view.</returns>
        public override string ToString()
        {
            return $"{ptr} [{extent}]";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second views are the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second views are the same.</returns>
        public static bool operator ==(ArrayView<T, TIndex> first, ArrayView<T, TIndex> second)
        {
            return first.ptr == second.ptr && first.extent.Equals(second.extent);
        }

        /// <summary>
        /// Returns true iff the first and second view are not the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second view are not the same.</returns>
        public static bool operator !=(ArrayView<T, TIndex> first, ArrayView<T, TIndex> second)
        {
            return first.ptr != second.ptr || !first.extent.Equals(second.extent);
        }

        #endregion
    }
}
