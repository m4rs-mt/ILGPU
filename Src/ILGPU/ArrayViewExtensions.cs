// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ArrayViewExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Array view extension methods
    /// </summary>
    public static partial class ArrayViewExtensions
    {
        #region ArrayView

        /// <summary>
        /// Loads the effective address of the current view.
        /// This operation is not supported on accelerators.
        /// </summary>
        /// <remarks>
        /// Use with caution since this operation does not make sense with respect to all
        /// target platforms.
        /// </remarks>
        /// <returns>The effective address.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void* LoadEffectiveAddress<T>(this ArrayView<T> view)
            where T : unmanaged =>
            view.LoadEffectiveAddress();

        /// <summary>
        /// Aligns the given array view to the specified alignment in bytes and returns a
        /// view spanning the initial unaligned parts of the given view and another
        /// view (main) spanning the remaining aligned elements of the given view.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <param name="alignmentInBytes">The basic alignment in bytes.</param>
        /// <returns>
        /// The prefix and main views pointing to non-aligned and aligned sub-views of
        /// the given view.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ArrayView<T> prefix, ArrayView<T> main) AlignTo<T>(
            this ArrayView<T> view,
            int alignmentInBytes)
            where T : unmanaged
        {
            Trace.Assert(
                alignmentInBytes > 0 &
                (alignmentInBytes % Interop.SizeOf<T>() == 0),
                "Invalid alignment in bytes");

            return view.AlignToInternal(alignmentInBytes);
        }

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The converted 2D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView2D<T> As2DView<T>(this ArrayView<T> view, long height)
            where T : unmanaged =>
            new ArrayView2D<T>(view, height);

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The converted 2D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView2D<T> As2DView<T>(
            this ArrayView<T> view,
            long width,
            long height)
            where T : unmanaged =>
            new ArrayView2D<T>(view, width, height);

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="extent">The extent.</param>
        /// <returns>The converted 2D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView2D<T> As2DView<T>(
            this ArrayView<T> view,
            LongIndex2 extent)
            where T : unmanaged =>
            new ArrayView2D<T>(view, extent);

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView3D<T> As3DView<T>(
            this ArrayView<T> view,
            long height,
            long depth)
            where T : unmanaged =>
            new ArrayView3D<T>(view, height, depth);

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="extent">
        /// The height (number of elements in y direction) and depth (number of elements
        /// in z direction).
        /// </param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView3D<T> As3DView<T>(
            this ArrayView<T> view,
            LongIndex2 extent)
            where T : unmanaged =>
            new ArrayView3D<T>(view, extent.X, extent.Y);

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView3D<T> As3DView<T>(
            this ArrayView<T> view,
            long width,
            long height,
            long depth)
            where T : unmanaged =>
            new ArrayView3D<T>(view, width, height, depth);

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="extent">
        /// The width (number of elements in x direction) and height (number of elements
        /// in y direction).
        /// </param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView3D<T> As3DView<T>(
            this ArrayView<T> view,
            LongIndex2 extent,
            long depth)
            where T : unmanaged =>
            new ArrayView3D<T>(view, extent, depth);

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="extent">
        /// The height (number of elements in y direction) and depth (number of elements
        /// in z direction).
        /// </param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView3D<T> As3DView<T>(
            this ArrayView<T> view,
            long width,
            LongIndex2 extent)
            where T : unmanaged =>
            new ArrayView3D<T>(view, width, extent);

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="extent">The extent.</param>
        /// <returns>The converted 3D view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView3D<T> As3DView<T>(
            this ArrayView<T> view,
            LongIndex3 extent)
            where T : unmanaged =>
            new ArrayView3D<T>(view, extent);

        /// <summary>
        /// Returns a variable view to the first element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <returns>The resolved variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableView<T> GetVariableView<T>(this ArrayView<T> view)
            where T : unmanaged =>
            view.GetVariableView(Index1.Zero);

        /// <summary>
        /// Returns a variable view to the given element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="element">The element index.</param>
        /// <returns>The resolved variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableView<T> GetVariableView<T>(
            this ArrayView<T> view,
            Index1 element)
            where T : unmanaged =>
            new VariableView<T>(view.GetSubView(element, 1));

        /// <summary>
        /// Returns a variable view to the given element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="element">The element index.</param>
        /// <returns>The resolved variable view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableView<T> GetVariableView<T>(
            this ArrayView<T> view,
            LongIndex1 element)
            where T : unmanaged =>
            new VariableView<T>(view.GetSubView(element, 1L));

        #endregion
    }

    partial struct ArrayView2D<T>
    {
        #region Instance

        /// <summary>
        /// Constructs a new 2D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView2D(ArrayView<T> view, long height)
            : this(view, new LongIndex2(view.Length / height, height))
        { }

        /// <summary>
        /// Constructs a new 2D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView2D(ArrayView<T> view, long width, long height)
            : this(view, new LongIndex2(width, height))
        { }

        /// <summary>
        /// Constructs a new 2D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="extent">The extent (width, height) (number of elements).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView2D(ArrayView<T> view, LongIndex2 extent)
            : this(new ArrayView<T, LongIndex2>(view, extent))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Width of this view.
        /// </summary>
        public readonly long Width => Extent.X;

        /// <summary>
        /// Returns the height of this view.
        /// </summary>
        public readonly long Height => Extent.Y;

        /// <summary>
        /// Returns the rows of this view that represents
        /// an implicitly transposed matrix.
        /// </summary>
        public readonly long Rows => Extent.X;

        /// <summary>
        /// Returns the columns of this view that represents
        /// an implicitly transposed matrix.
        /// </summary>
        public readonly long Columns => Extent.Y;

        /// <summary>
        /// Accesses the element at the given index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        /// <returns>The element at the given index.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1023:IndexersShouldNotBeMultidimensional")]
        public readonly ref T this[int x, int y] =>
            ref this[new Index2(x, y)];

        /// <summary>
        /// Accesses the element at the given index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        /// <returns>The element at the given index.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1023:IndexersShouldNotBeMultidimensional")]
        public readonly ref T this[long x, long y] =>
            ref this[new LongIndex2(x, y)];

        #endregion

        #region Methods

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <returns>A linear view to a single row.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> GetRowView(long y)
        {
            Trace.Assert(y >= 0 && y < Height, "y out of bounds");
            return AsLinearView().GetSubView(y * Width, Width);
        }

        #endregion
    }

    partial struct ArrayView3D<T>
    {
        #region Instance

        /// <summary>
        /// Constructs a new 3D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView3D(ArrayView<T> view, long height, long depth)
            : this(view, new LongIndex3(view.Length / (height * depth), height, depth))
        { }

        /// <summary>
        /// Constructs a new 3D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView3D(ArrayView<T> view, long width, long height, long depth)
            : this(view, new LongIndex3(width, height, depth))
        { }

        /// <summary>
        /// Constructs a new 3D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="extent">
        /// The width (number of elements in x direction) and height (number of elements
        /// in y direction).
        /// </param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView3D(ArrayView<T> view, LongIndex2 extent, long depth)
            : this(view, new LongIndex3(extent, depth))
        { }

        /// <summary>
        /// Constructs a new 3D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="extent">
        /// The height (number of elements in y direction) and depth (number of elements
        /// in z direction).
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView3D(ArrayView<T> view, long width, LongIndex2 extent)
            : this(view, new LongIndex3(width, extent))
        { }

        /// <summary>
        /// Constructs a new 3D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="extent">
        /// The extent (width, height, depth) (number of elements).
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView3D(ArrayView<T> view, LongIndex3 extent)
            : this(new ArrayView<T, LongIndex3>(view, extent))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Width of this view.
        /// </summary>
        public readonly long Width => Extent.X;

        /// <summary>
        /// Returns the height of this view.
        /// </summary>
        public readonly long Height => Extent.Y;

        /// <summary>
        /// Returns the depth of this view.
        /// </summary>
        public readonly long Depth => Extent.Z;

        /// <summary>
        /// Accesses the element at the given index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        /// <param name="z">The z index.</param>
        /// <returns>The element at the given index.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1023:IndexersShouldNotBeMultidimensional")]
        public readonly ref T this[int x, int y, int z] =>
            ref this[new Index3(x, y, z)];

        /// <summary>
        /// Accesses the element at the given index.
        /// </summary>
        /// <param name="xy">The x and y indices.</param>
        /// <param name="z">The z index.</param>
        /// <returns>The element at the given index.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1023:IndexersShouldNotBeMultidimensional")]
        public readonly ref T this[Index2 xy, int z] =>
            ref this[xy.X, xy.Y, z];

        /// <summary>
        /// Accesses the element at the given index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="yz">The z and y indices.</param>
        /// <returns>The element at the given index.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1023:IndexersShouldNotBeMultidimensional")]
        public readonly ref T this[int x, Index2 yz] =>
            ref this[x, yz.X, yz.Y];

        #endregion

        #region Methods

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="index">
        /// The y index of the row and the z index of the slice.
        /// </param>
        /// <returns>A linear view to a single row.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> GetRowView(LongIndex2 index) =>
            GetRowView(index.X, index.Y);

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A linear view to a single row.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView<T> GetRowView(long y, long z) =>
            GetSliceView(z).GetRowView(y);

        /// <summary>
        /// Returns a 2D view to a single slice.
        /// </summary>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A 2D view to a single slice.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayView2D<T> GetSliceView(long z)
        {
            Trace.Assert(z >= 0 && z < Depth, "z out of bounds");
            return new ArrayView2D<T>(
                BaseView.AsLinearView().GetSubView(z * Width * Height, Width * Height),
                Height);
        }

        #endregion
    }
}
