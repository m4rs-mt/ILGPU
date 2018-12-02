// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ArrayViewGeneric.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Represents a generic view to an n-dimensional chunk of memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    [DebuggerTypeProxy(typeof(DebugArrayView<,>))]
    [DebuggerDisplay("Index = {Index}, Extent = {Extent}")]
    public readonly struct ArrayView<T, TIndex> : IArrayView<T, TIndex>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        #region Constants

        /// <summary>
        /// Represents the native size of a single element.
        /// </summary>
        public static readonly int ElementSize = ArrayView<T>.ElementSize;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new array view.
        /// </summary>
        /// <param name="baseView">The base view.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public ArrayView(ArrayView<T> baseView, TIndex extent)
        {
            Debug.Assert(baseView.Length <= extent.Size, "Extent out of range");
            BaseView = baseView;
            Extent = extent;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the base view.
        /// </summary>
        public ArrayView<T> BaseView { get; }

        /// <summary>
        /// Returns true iff this view points to a valid location.
        /// </summary>
        public bool IsValid => BaseView.IsValid;

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        public int Length => BaseView.Length;

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public TIndex Extent { get; }

        /// <summary>
        /// Returns the length of this array view in bytes.
        /// </summary>
        public int LengthInBytes => Length * ElementSize;

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public ref T this[TIndex index] => ref BaseView[index.ComputeLinearIndex(Extent)];

        #endregion

        #region Methods

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T, TIndex> GetSubView(TIndex index)
        {
            Debug.Assert(index.InBounds(Extent), "Offset ouf of bounds");
            return GetSubView(index, Extent.Subtract(index));
        }

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <param name="subViewExtent">The extent of the new subview.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T, TIndex> GetSubView(TIndex index, TIndex subViewExtent)
        {
            Debug.Assert(index.InBounds(Extent), "Offset ouf of bounds");
            var elementIndex = index.ComputeLinearIndex(Extent);
            Debug.Assert(elementIndex >= 0 && elementIndex < Length, "Offset ouf of bounds");
            Debug.Assert(index.Add(subViewExtent).InBoundsInclusive(Extent), "Subview out of range");
            var subView = BaseView.GetSubView(elementIndex, subViewExtent.Size);
            return new ArrayView<T, TIndex>(subView, subViewExtent);
        }

        /// <summary>
        /// Casts the current array view into another array-view type.
        /// </summary>
        /// <typeparam name="TOther">The target type.</typeparam>
        /// <returns>The casted array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<TOther, TIndex> Cast<TOther>()
            where TOther : struct
        {
            var newExtent = Extent.ComputedCastedExtent(
                Extent, ElementSize, ArrayView<TOther, TIndex>.ElementSize);
            return new ArrayView<TOther, TIndex>(
                BaseView.Cast<TOther>(), newExtent);
        }

        /// <summary>
        /// Returns a variable view that points to the element at the specified index.
        /// </summary>
        /// <param name="index">The variable index.</param>
        /// <returns>The resolved variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VariableView<T> GetVariableView(TIndex index)
        {
            return new VariableView<T>(
                BaseView.GetSubView(index.ComputeLinearIndex(Extent), 1));
        }

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        public ArrayView<T> AsLinearView() => BaseView;

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this view.
        /// </summary>
        /// <returns>The string representation of this view.</returns>
        public override string ToString()
        {
            return BaseView.ToString();
        }

        #endregion
    }
}
