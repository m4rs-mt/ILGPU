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
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// An abstract untyped basic array view.
    /// </summary>
    public interface IArrayView
    {
        /// <summary>
        /// Returns the underlying managed buffer.
        /// </summary>
        /// <remarks>This property is not supported on accelerators.</remarks>
        MemoryBuffer Buffer { get; }

        /// <summary>
        /// Returns true if this view points to a valid location.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Returns the length of this array view.
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Returns the element size.
        /// </summary>
        int ElementSize { get; }

        /// <summary>
        /// Returns the length of this array view in bytes.
        /// </summary>
        long LengthInBytes { get; }
    }

    /// <summary>
    /// Represents a contiguous array view.
    /// </summary>
    public interface IContiguousArrayView : IArrayView
    {
        /// <summary>
        /// Returns the index pointing into the parent buffer.
        /// </summary>
        /// <remarks>This property is not supported on accelerators.</remarks>
        long Index { get; }

        /// <summary>
        /// Returns the index in bytes of the given view.
        /// </summary>
        /// <returns>The index in bytes of the given view.</returns>
        /// <remarks>This property is not supported on accelerators.</remarks>
        long IndexInBytes { get; }

        /// <summary>
        /// Returns the raw array view pointing to this view.
        /// </summary>
        /// <returns>The raw array view.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        ArrayView<byte> AsRawArrayView();
    }

    /// <summary>
    /// An abstract typed array view used for generic constraints.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IArrayView<T> : IArrayView
        where T : unmanaged
    { }

    /// <summary>
    /// Represents a contiguous array view.
    /// </summary>
    public interface IContiguousArrayView<T> : IArrayView<T>, IContiguousArrayView
        where T : unmanaged
    { }

    /// <summary>
    /// An array view that uses an n-Dimensional stride.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStrideIndex">The underlying stride dimension index.</typeparam>
    /// <typeparam name="TStride">The stride type.</typeparam>
    public interface IStridedArrayView<T, TStrideIndex, TStride> : IArrayView<T>
        where T : unmanaged
        where TStrideIndex : struct, IGenericIndex<TStrideIndex>
        where TStride : struct, IStride<TStrideIndex>
    {
        /// <summary>
        /// Returns the current stride.
        /// </summary>
        TStride Stride { get; }
    }

    /// <summary>
    /// Represents an abstract array view.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    public interface IArrayView<T, TIndex> : IArrayView
        where T : unmanaged
        where TIndex : struct, IGenericIndex<TIndex>
    {
        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        TIndex Extent { get; }

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
    /// <typeparam name="TIndex">The 32-bit index type.</typeparam>
    /// <typeparam name="TLongIndex">The 64-bit index type.</typeparam>
    /// <typeparam name="TStride">The stride type.</typeparam>
    public interface IArrayView<T, TIndex, TLongIndex, TStride> :
        IArrayView<T, TLongIndex>,
        IStridedArrayView<T, TIndex, TStride>
        where T : unmanaged
        where TIndex : struct, IIntIndex<TIndex, TLongIndex>
        where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
        where TStride : struct, IStride<TIndex>
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
    /// Represents a generic view to a contiguous chunk of memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [DebuggerTypeProxy(typeof(DebugArrayView<>))]
    [DebuggerDisplay("Extent = {Extent}, Length = {Length}")]
    public readonly struct ArrayView<T> :
        IArrayView<T, Index1D, LongIndex1D, Stride1D.Dense>,
        IContiguousArrayView<T>
        where T : unmanaged
    {
        #region Static

        /// <summary>
        /// Represents the native size of a single element.
        /// </summary>
        public static readonly int ElementSize = Interop.SizeOf<T>();

        /// <summary>
        /// Represents an empty view that is not valid and has a length of 0 elements.
        /// </summary>
        public static readonly ArrayView<T> Empty;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new array view.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="index">The base index.</param>
        /// <param name="length">The extent (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [NotInsideKernel]
        public ArrayView(MemoryBuffer source, long index, long length)
        {
            Trace.Assert(source != null, "Invalid source buffer");
            Trace.Assert(index >= 0L, "Index out of range");
            Trace.Assert(length >= 0L, "Length out of range");

            Buffer = source;
            Index = index;
            Length = length;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated buffer.
        /// </summary>
        /// <remarks>This property is not supported on accelerators.</remarks>
        internal MemoryBuffer Buffer { get; }

        /// <summary>
        /// A private interface implementation of the <see cref="Buffer"/> property.
        /// </summary>
        /// <remarks>This property is not supported on accelerators.</remarks>
        readonly MemoryBuffer IArrayView.Buffer => Buffer;

        /// <summary>
        /// Returns the index of this view.
        /// </summary>
        internal long Index { get; }

        /// <summary>
        /// Returns the index pointing into the parent buffer.
        /// </summary>
        /// <remarks>This property is not supported on accelerators.</remarks>
        readonly long IContiguousArrayView.Index
        {
            [NotInsideKernel]
            get => Index;
        }

        /// <summary>
        /// Returns the index in bytes of the given view.
        /// </summary>
        /// <returns>The index in bytes of the given view.</returns>
        /// <remarks>This property is not supported on accelerators.</remarks>
        readonly long IContiguousArrayView.IndexInBytes
        {
            [NotInsideKernel]
            get => GetIndexInBytes();
        }

        /// <summary>
        /// Returns the statically known element size.
        /// </summary>
        readonly int IArrayView.ElementSize => ElementSize;

        /// <summary>
        /// Returns true if this view points to a valid location.
        /// </summary>
        public readonly bool IsValid
        {
            [ViewIntrinsic(ViewIntrinsicKind.IsValidView)]
            get => Buffer != null && Length > 0;
        }

        /// <summary>
        /// Ensures that the current view is valid CPU buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void EnsureCPUBuffer() =>
            Trace.Assert(
                Buffer.AcceleratorType == AcceleratorType.CPU,
                "Cannot access a non-CPU buffer directly");

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public readonly LongIndex1D Extent
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetViewLongExtent)]
            get => new LongIndex1D(Length);
        }

        /// <summary>
        /// Returns the dense stride of this view.
        /// </summary>
        public readonly Stride1D.Dense Stride
        {
            [ViewIntrinsic(ViewIntrinsicKind.GetStride)]
            get => new Stride1D.Dense();
        }

        /// <summary>
        /// Returns the extent of this view.
        /// </summary>
        public readonly Index1D IntExtent
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
        public readonly unsafe ref T this[Index1D index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ViewIntrinsic(ViewIntrinsicKind.GetViewElementAddressByIndex)]
            get => ref this[(long)index.X];
        }

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public readonly unsafe ref T this[LongIndex1D index]
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
            get => ref this[(long)index];
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
                ref var ptr = ref LoadEffectiveAddress(index);
                return ref Unsafe.As<byte, T>(ref ptr);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the index in bytes of the given view.
        /// </summary>
        /// <returns>The index in bytes of the given view.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        internal readonly long GetIndexInBytes() => Index * ElementSize;

        /// <summary>
        /// Converts the given generic array view into a raw view of bytes.
        /// </summary>
        /// <returns>The raw array view.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        readonly ArrayView<byte> IContiguousArrayView.AsRawArrayView() =>
            new ArrayView<byte>(Buffer, GetIndexInBytes(), LengthInBytes);

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
        internal readonly unsafe (ArrayView<T> prefix, ArrayView<T> main)
            AlignToInternal(
            int alignmentInBytes)
        {
            long elementsToSkip = IntrinsicMath.Min(
                Interop.ComputeAlignmentOffset(
                    LoadEffectiveAddressAsPtr().ToInt64(),
                    alignmentInBytes) / Interop.SizeOf<T>(),
                Length);

            return (
                new ArrayView<T>(Buffer, 0, elementsToSkip),
                new ArrayView<T>(Buffer, elementsToSkip, Length - elementsToSkip));
        }

        /// <summary>
        /// Loads the effective address of the current view.
        /// </summary>
        /// <param name="index">The relative element index.</param>
        /// <returns>The effective address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly unsafe ref byte LoadEffectiveAddress(long index) =>
            ref Interop.ComputeEffectiveAddress(
                ref Unsafe.AsRef<byte>(Buffer.NativePtr.ToPointer()),
                Index + index,
                ElementSize);

        /// <summary>
        /// Loads the effective address of the current view.
        /// </summary>
        /// <param name="index">The relative element index.</param>
        /// <returns>The effective address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly unsafe IntPtr LoadEffectiveAddressAsPtr(long index) =>
            new IntPtr(Unsafe.AsPointer(ref LoadEffectiveAddress(index)));

        /// <summary>
        /// Loads the effective address of the current view.
        /// </summary>
        /// <returns>The effective address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly unsafe ref byte LoadEffectiveAddress() =>
            ref LoadEffectiveAddress(0L);

        /// <summary>
        /// Loads the effective address of the current view.
        /// </summary>
        /// <returns>The effective address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly unsafe IntPtr LoadEffectiveAddressAsPtr() =>
            LoadEffectiveAddressAsPtr(0L);

        /// <summary>
        /// Returns a sub view of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <returns>The new sub view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetSubViewImplicitLength)]
        public readonly ArrayView<T> SubView(long index)
        {
            Trace.Assert(
                index >= 0 && index < Length || index == 0 && Length < 1,
                "Offset out of bounds");
            return SubView(index, Length - index);
        }

        /// <summary>
        /// Returns a sub view of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <param name="subViewLength">The extent of the new sub view.</param>
        /// <returns>The new sub view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetSubView)]
        public readonly ArrayView<T> SubView(int index, int subViewLength) =>
            SubView((long)index, subViewLength);

        /// <summary>
        /// Returns a sub view of the current view starting at the given offset.
        /// </summary>
        /// <param name="index">The starting offset.</param>
        /// <param name="subViewLength">The extent of the new sub view.</param>
        /// <returns>The new sub view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.GetSubView)]
        public readonly ArrayView<T> SubView(long index, long subViewLength)
        {
            Trace.Assert(
                index >= 0 && index < Length || index == 0 && Length < 1,
                "Index out of bounds");
            Trace.Assert(index + subViewLength <= Length, "Sub view out of range");
            index += Index;
            return new ArrayView<T>(Buffer, index, subViewLength);
        }

        /// <summary>
        /// Casts the current array view into another array-view type.
        /// </summary>
        /// <typeparam name="TOther">The target type.</typeparam>
        /// <returns>The casted array view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ViewIntrinsic(ViewIntrinsicKind.CastView)]
        public readonly ArrayView<TOther> Cast<TOther>()
            where TOther : unmanaged
        {
            var castContext = StrideExtensions.CreateCastContext(
                ElementSize,
                ArrayView<TOther>.ElementSize);
            long newExtent = castContext.ComputeNewExtent(Extent);
            long newIndex = castContext.ComputeNewExtent(Index);
            return new ArrayView<TOther>(Buffer, newIndex, newExtent);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this view.
        /// </summary>
        /// <returns>The string representation of this view.</returns>
        /// <remarks>This method is not supported on accelerators.</remarks>
        [NotInsideKernel]
        public readonly override string ToString() => $"{Index} [{Length}]";

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given specialized array view into a corresponding generic view.
        /// </summary>
        /// <returns>The corresponding generic view.</returns>
        public static implicit operator ArrayView1D<T, Stride1D.Dense>(
            ArrayView<T> view) =>
            new ArrayView1D<T, Stride1D.Dense>(view, view.Extent, default);

        /// <summary>
        /// Converts the given specialized array view into a corresponding generic view.
        /// </summary>
        /// <returns>The corresponding generic view.</returns>
        public static implicit operator ArrayView<T>(
            ArrayView1D<T, Stride1D.Dense> view) =>
            view.BaseView.SubView(0, view.Length);

        #endregion
    }
}
