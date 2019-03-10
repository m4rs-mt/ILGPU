// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ArrayView.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.Runtime;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Represents an abstract array view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    public interface IArrayView<T, TIndex>
        where T : struct
        where TIndex : struct, IIndex, IGenericIndex<TIndex>
    {
        /// <summary>
        /// Returns true iff this view points to a valid location.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        TIndex Extent { get; }

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Returns the length of this array view in bytes.
        /// </summary>
        int LengthInBytes { get; }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        ref T this[TIndex index] { get; }

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        ArrayView<T> AsLinearView();
    }

    /// <summary>
    /// Represents an abstract array view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [SuppressMessage("Microsoft.Design", "CA1040: AvoidEmptyInterfaces",
        Justification = "Can be used in generic constraints")]
    public interface IArrayView<T> : IArrayView<T, Index>
        where T : struct
    { }

    /// <summary>
    /// Represents a generic view to a contiguous chunk of memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [DebuggerTypeProxy(typeof(DebugArrayView<>))]
    [DebuggerDisplay("Extent = {Extent}, Length = {Length}")]
    public readonly struct ArrayView<T> : IArrayView<T>
        where T : struct
    {
        #region Constants

        /// <summary>
        /// Represents the native size of a single element.
        /// </summary>
        public static readonly int ElementSize = Interop.SizeOf<T>();

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new array view.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="index">The base index.</param>
        /// <param name="length">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView(ArrayViewSource source, Index index, Index length)
        {
            Debug.Assert(source != null, "Invalid source buffer");
            Debug.Assert(index >= 0, "Index of of range");
            Debug.Assert(length > 0, "Length of of range");
            Source = source;
            Index = index;
            Length = length;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated view source.
        /// </summary>
        internal ArrayViewSource Source { get; }

        /// <summary>
        /// Returns the associated accelerator type.
        /// </summary>
        internal AcceleratorType AcceleratorType => Source.AcceleratorType;

        /// <summary>
        /// Returns true iff this view points to a valid location.
        /// </summary>
        public bool IsValid
        {
            [ViewIntrinsic(ViewIntrinsicKind.IsValidView)]
            get => Source != null && Length > 0;
        }

        /// <summary>
        /// Returns the index of this view.
        /// </summary>
        internal Index Index { get; }

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public Index Extent
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewExtent)]
            get => new Index(Length);
        }

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        public int Length
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewLength)]
            get;
        }

        /// <summary>
        /// Returns the length of this array view in bytes.
        /// </summary>
        public int LengthInBytes
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewLengthInBytes)]
            get => Length * ElementSize;
        }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public unsafe ref T this[Index index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ViewIntrinsic(ViewIntrinsicKind.GetViewElementAddressByIndex)]
            get => ref this[index.X];
        }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public unsafe ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ViewIntrinsic(ViewIntrinsicKind.GetViewElementAddress)]
            get
            {
                Debug.Assert(index >= 0 && index < Length, "Index out of range");
                Debug.Assert(
                    Source.AcceleratorType == AcceleratorType.CPU,
                    "Cannot access a non-CPU buffer directly");
                ref var ptr = ref Source.LoadEffectiveAddress(
                    Index + index,
                    ElementSize);
                return ref Unsafe.As<byte, T>(ref ptr);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads the effective address of the current view.
        /// </summary>
        /// <returns>The effective address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void* LoadEffectiveAddress() =>
            Unsafe.AsPointer(ref Source.LoadEffectiveAddress(Index, ElementSize));

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetSubViewImplicitLength)]
        public ArrayView<T> GetSubView(int index)
        {
            Debug.Assert(index >= 0 && index < Length, "Offset ouf of bounds");
            return GetSubView(index, Length - index);
        }

        /// <summary>
        /// Returns a subview of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <param name="subViewLength">The extent of the new subview.</param>
        /// <returns>The new subview.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetSubView)]
        public ArrayView<T> GetSubView(int index, int subViewLength)
        {
            Debug.Assert(index >= 0 && index < Length, "Index out of bounds");
            Debug.Assert(index < Length, "Index ouf of bounds");
            Debug.Assert(index + subViewLength <= Length, "Subview out of range");
            index += Index;
            return new ArrayView<T>(Source, index, subViewLength);
        }

        /// <summary>
        /// Casts the current array view into another array-view type.
        /// </summary>
        /// <typeparam name="TOther">The target type.</typeparam>
        /// <returns>The casted array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.CastView)]
        public ArrayView<TOther> Cast<TOther>()
            where TOther : struct
        {
            var extent = new Index(Length);
            var newExtent = extent.ComputedCastedExtent(
                extent, ElementSize, ArrayView<TOther>.ElementSize);
            var newIndex = extent.ComputedCastedExtent(
                Index, ElementSize, ArrayView<TOther>.ElementSize);
            return new ArrayView<TOther>(Source, newIndex, newExtent);
        }

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.AsLinearView)]
        public ArrayView<T> AsLinearView() => this;

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this view.
        /// </summary>
        /// <returns>The string representation of this view.</returns>
        public override string ToString()
        {
            return $"{Index} [{Length}]";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Converts a linear view to its explicit form.
        /// </summary>
        /// <param name="view">The view to convert.</param>
        public static implicit operator ArrayView<T, Index>(ArrayView<T> view)
        {
            return new ArrayView<T, Index>(view, view.Length);
        }

        /// <summary>
        /// Converts a linear view from its explicit form.
        /// </summary>
        /// <param name="view">The view to convert.</param>
        public static implicit operator ArrayView<T>(ArrayView<T, Index> view)
        {
            return view.AsLinearView();
        }

        #endregion
    }
}
