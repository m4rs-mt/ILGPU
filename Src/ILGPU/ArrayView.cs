// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ArrayView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
        where T : unmanaged
        where TIndex : struct, IGenericIndex<TIndex>
    {
        /// <summary>
        /// Returns true if this view points to a valid location.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        TIndex Extent { get; }

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Returns the length of this array view in bytes.
        /// </summary>
        long LengthInBytes { get; }

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
    /// <typeparam name="TIndex">The 32-bit index type.</typeparam>
    /// <typeparam name="TLongIndex">The 64-bit index type.</typeparam>
    public interface IArrayView<T, TIndex, TLongIndex> : IArrayView<T, TLongIndex>
        where T : unmanaged
        where TIndex : struct, IIntIndex<TIndex, TLongIndex>
        where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
    {
        /// <summary>
        /// Returns the 32-bit length of this array view.
        /// </summary>
        int IntLength { get; }

        /// <summary>
        /// Returns the 32-bit extent of this view.
        /// </summary>
        TIndex IntExtent { get; }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        ref T this[TIndex index] { get; }
    }

    /// <summary>
    /// Represents an abstract array view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [SuppressMessage(
        "Microsoft.Design",
        "CA1040: AvoidEmptyInterfaces",
        Justification = "Can be used in generic constraints")]
    public interface IArrayView<T> : IArrayView<T, Index1, LongIndex1>
        where T : unmanaged
    { }

    /// <summary>
    /// Represents a generic view to a contiguous chunk of memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [DebuggerTypeProxy(typeof(DebugArrayView<>))]
    [DebuggerDisplay("Extent = {Extent}, Length = {Length}")]
    public readonly struct ArrayView<T> : IArrayView<T, Index1, LongIndex1>
        where T : unmanaged
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
        public ArrayView(ArrayViewSource source, long index, long length)
        {
            Trace.Assert(source != null, "Invalid source buffer");
            Trace.Assert(index >= 0L, "Index out of range");
            Trace.Assert(length >= 0L, "Length out of range");
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
        /// Returns true if this view points to a valid location.
        /// </summary>
        public readonly bool IsValid
        {
            [ViewIntrinsic(ViewIntrinsicKind.IsValidView)]
            get => Source != null && Length > 0;
        }

        /// <summary>
        /// Returns the index of this view.
        /// </summary>
        internal long Index { get; }

        /// <summary>
        /// Ensures that the current view is valid CPU buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void EnsureCPUBuffer() =>
            Trace.Assert(
                Source.AcceleratorType == AcceleratorType.CPU,
                "Cannot access a non-CPU buffer directly");

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public readonly LongIndex1 Extent
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewLongExtent)]
            get => new LongIndex1(Length);
        }

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public readonly Index1 IntExtent
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewExtent)]
            get => Extent.ToIntIndex();
        }

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        public readonly long Length
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewLongLength)]
            get;
        }

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        public readonly int IntLength
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewLength)]
            get
            {
                IndexTypeExtensions.AssertIntIndexRange(Length);
                return (int)Length;
            }
        }

        /// <summary>
        /// Returns the length of this array view in bytes.
        /// </summary>
        public readonly long LengthInBytes
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewLengthInBytes)]
            get => Length * ElementSize;
        }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public readonly unsafe ref T this[Index1 index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ViewIntrinsic(ViewIntrinsicKind.GetViewElementAddressByIndex)]
            get
            {
                IndexTypeExtensions.AssertIntIndex(Length);
                return ref this[(long)index.X];
            }
        }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public readonly unsafe ref T this[LongIndex1 index]
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
        public readonly unsafe ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ViewIntrinsic(ViewIntrinsicKind.GetViewElementAddress)]
            get
            {
                IndexTypeExtensions.AssertIntIndex(Length);
                return ref this[(long)index];
            }
        }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public readonly unsafe ref T this[long index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ViewIntrinsic(ViewIntrinsicKind.GetViewElementAddress)]
            get
            {
                Trace.Assert(index >= 0 && index < Length, "Index out of range");
                EnsureCPUBuffer();
                ref var ptr = ref Source.LoadEffectiveAddress(
                    Index + index,
                    ElementSize);
                return ref Unsafe.As<byte, T>(ref ptr);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Aligns the current array view to the given alignment in bytes and returns a
        /// view spanning the initial unaligned parts of the current view and another
        /// view (main) spanning the remaining aligned elements of the current view.
        /// </summary>
        /// <param name="alignmentInBytes">The basic alignment in bytes.</param>
        /// <returns>
        /// The prefix and main views pointing to non-aligned and aligned sub-views of
        /// this view.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.AlignTo)]
        internal unsafe (ArrayView<T> prefix, ArrayView<T> main) AlignToInternal(
            int alignmentInBytes)
        {
            long elementsToSkip = IntrinsicMath.Min(
                Interop.ComputeAlignmentOffset(
                    (long)LoadEffectiveAddress(),
                    alignmentInBytes) / Interop.SizeOf<T>(),
                Length);

            return (
                new ArrayView<T>(Source, 0, elementsToSkip),
                new ArrayView<T>(Source, elementsToSkip, Length - elementsToSkip));
        }

        /// <summary>
        /// Loads a linear element address using the given multi-dimensional indices.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="index">The element index.</param>
        /// <param name="dimension">The dimension specifications.</param>
        /// <returns>A reference to the i-th element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetViewLinearElementAddress)]
        internal readonly unsafe ref T GetLinearElementAddress<TIndex>(
            TIndex index,
            TIndex dimension)
            where TIndex : struct, IGenericIndex<TIndex> =>
            ref this[index.ComputeLongLinearIndex(dimension)];

        /// <summary>
        /// Loads the effective address of the current view.
        /// </summary>
        /// <returns>The effective address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void* LoadEffectiveAddress() =>
            Unsafe.AsPointer(ref Source.LoadEffectiveAddress(Index, ElementSize));

        /// <summary>
        /// Returns a sub view of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <returns>The new sub view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetSubViewImplicitLength)]
        public ArrayView<T> GetSubView(long index)
        {
            Trace.Assert(index >= 0 && index < Length, "Offset out of bounds");
            return GetSubView(index, Length - index);
        }

        /// <summary>
        /// Returns a sub view of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <param name="subViewLength">The extent of the new sub view.</param>
        /// <returns>The new sub view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetSubView)]
        public ArrayView<T> GetSubView(int index, int subViewLength)
        {
            IndexTypeExtensions.AssertIntIndexRange(Length);
            return GetSubView((long)index, subViewLength);
        }

        /// <summary>
        /// Returns a sub view of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <param name="subViewLength">The extent of the new sub view.</param>
        /// <returns>The new sub view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetSubView)]
        public ArrayView<T> GetSubView(long index, long subViewLength)
        {
            Trace.Assert(index >= 0 && index < Length, "Index out of bounds");
            Trace.Assert(index < Length, "Index out of bounds");
            Trace.Assert(index + subViewLength <= Length, "Sub view out of range");
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
            where TOther : unmanaged
        {
            var extent = Extent;
            long newExtent = extent.ComputedCastedExtent(
                extent, ElementSize, ArrayView<TOther>.ElementSize);
            long newIndex = extent.ComputedCastedExtent(
                Index, ElementSize, ArrayView<TOther>.ElementSize);
            return new ArrayView<TOther>(Source, newIndex, newExtent);
        }

        /// <summary>
        /// Returns the associated 32-buffer view.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T, Index1> ToIntView() =>
            new ArrayView<T, Index1>(AsLinearView(), IntExtent);

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        [ViewIntrinsic(ViewIntrinsicKind.AsLinearView)]
        public ArrayView<T> AsLinearView() => this;

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this view.
        /// </summary>
        /// <returns>The string representation of this view.</returns>
        public override string ToString() => $"{Index} [{Length}]";

        #endregion

        #region Operators

        /// <summary>
        /// Converts a generic array view into an intrinsic array view.
        /// </summary>
        /// <param name="view">The view instance to convert.</param>
        public static implicit operator ArrayView<T, Index1>(
            ArrayView<T> view) =>
            new ArrayView<T, Index1>(view, view.IntExtent);

        /// <summary>
        /// Converts an intrinsic array view into a generic array view.
        /// </summary>
        /// <param name="view">The view instance to convert.</param>
        /// <remarks>Required due to backwards compatibility.</remarks>
        public static implicit operator ArrayView<T, LongIndex1>(
            ArrayView<T> view) =>
            new ArrayView<T, LongIndex1>(view, view.Extent);

        /// <summary>
        /// Converts an intrinsic array view into a generic array view.
        /// </summary>
        /// <param name="view">The view instance to convert.</param>
        public static implicit operator ArrayView<T>(
            ArrayView<T, Index1> view) =>
            view.BaseView;

        /// <summary>
        /// Converts an intrinsic array view into a generic array view.
        /// </summary>
        /// <param name="view">The view instance to convert.</param>
        public static implicit operator ArrayView<T>(
            ArrayView<T, LongIndex1> view) =>
            view.BaseView;

        #endregion
    }

    /// <summary>
    /// Represents a generic view to an n-dimensional chunk of memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The integer index type.</typeparam>
    [DebuggerTypeProxy(typeof(DebugArrayView<,>))]
    [DebuggerDisplay("Index = {Index}, Extent = {Extent}")]
    public readonly struct ArrayView<T, TIndex> : IArrayView<T, TIndex>
        where T : unmanaged
        where TIndex : struct, IGenericIndex<TIndex>
    {
        #region Constants

        /// <summary>
        /// Represents the native size of a single element.
        /// </summary>
        public static int ElementSize => ArrayView<T>.ElementSize;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new array view.
        /// </summary>
        /// <param name="baseView">The base view.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public ArrayView(ArrayView<T> baseView, TIndex extent)
        {
            Trace.Assert(baseView.Length <= extent.Size, "Extent out of range");
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
        /// Returns true if this view points to a valid location.
        /// </summary>
        public readonly bool IsValid => BaseView.IsValid;

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        public readonly long Length => BaseView.Length;

        /// <summary>
        /// Returns the 32-bit length of this array view.
        /// </summary>
        public readonly int IntLength => BaseView.IntLength;

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public TIndex Extent { get; }

        /// <summary>
        /// Returns the length of this array view in bytes.
        /// </summary>
        public readonly long LengthInBytes => BaseView.LengthInBytes;

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public readonly ref T this[TIndex index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref BaseView.GetLinearElementAddress(index, Extent);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a sub view of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <param name="subViewExtent">The extent of the new sub view.</param>
        /// <returns>The new raw sub view.</returns>
        /// <remarks>
        /// Note that this function interprets the memory view as a linear contiguous
        /// chunk of memory that does not pay attention to the actual
        /// <see cref="Extent"/>. Instead, it converts the (potentially multidimensional)
        /// indices to linear indices and returns a raw view that spans a contiguous
        /// region of memory.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> GetSubView(TIndex index, LongIndex1 subViewExtent)
        {
            Trace.Assert(index.InBounds(Extent), "Offset out of bounds");
            var elementIndex = index.ComputeLongLinearIndex(Extent);
            Trace.Assert(
                elementIndex >= 0 && elementIndex < Length,
                "Offset out of bounds");
            Trace.Assert(
                elementIndex + (long)subViewExtent <= Length,
                "Sub view out of range");
            return BaseView.GetSubView(elementIndex, subViewExtent);
        }

        /// <summary>
        /// Casts the current array view into another array-view type.
        /// </summary>
        /// <typeparam name="TOther">The target type.</typeparam>
        /// <returns>The casted array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<TOther, TIndex> Cast<TOther>()
            where TOther : unmanaged
        {
            var newExtent = Extent.ComputedCastedExtent(
                Extent,
                ElementSize,
                ArrayView<TOther, TIndex>.ElementSize);
            return new ArrayView<TOther, TIndex>(
                BaseView.Cast<TOther>(), newExtent);
        }

        /// <summary>
        /// Returns a variable view that points to the element at the specified index.
        /// </summary>
        /// <param name="index">The variable index.</param>
        /// <returns>The resolved variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VariableView<T> GetVariableView(TIndex index) =>
            new VariableView<T>(
                BaseView.GetSubView(
                    index.ComputeLongLinearIndex(Extent),
                    1L));

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
        public override string ToString() => BaseView.ToString();

        #endregion
    }
}
