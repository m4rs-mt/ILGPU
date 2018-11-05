// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ArrayViewExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU
{
    /// <summary>
    /// Array view extension methods
    /// </summary>
    public static class ArrayViewExtensions
    {
        #region ArrayView

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The converted 2D view.</returns>
        public static ArrayView2D<T> As2DView<T>(this ArrayView<T> view, int height)
            where T : struct
        {
            return new ArrayView2D<T>(view, height);
        }

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The converted 2D view.</returns>
        public static ArrayView2D<T> As2DView<T>(this ArrayView<T> view, int width, int height)
            where T : struct
        {
            return new ArrayView2D<T>(view, width, height);
        }

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="extent">The extent.</param>
        /// <returns>The converted 2D view.</returns>
        public static ArrayView2D<T> As2DView<T>(this ArrayView<T> view, Index2 extent)
            where T : struct
        {
            return new ArrayView2D<T>(view, extent);
        }

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The converted 3D view.</returns>
        public static ArrayView3D<T> As3DView<T>(this ArrayView<T> view, int height, int depth)
            where T : struct
        {
            return new ArrayView3D<T>(view, height, depth);
        }

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The converted 3D view.</returns>
        public static ArrayView3D<T> As3DView<T>(this ArrayView<T> view, int width, int height, int depth)
            where T : struct
        {
            return new ArrayView3D<T>(view, width, height, depth);
        }

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="extent">The extent.</param>
        /// <returns>The converted 3D view.</returns>
        public static ArrayView3D<T> As3DView<T>(this ArrayView<T> view, Index3 extent)
            where T : struct
        {
            return new ArrayView3D<T>(view, extent);
        }

        /// <summary>
        /// Returns a variable view to the first element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <returns>The resolved variable view.</returns>
        public static VariableView<T> GetVariableView<T>(this ArrayView<T> view)
            where T : struct
        {
            return view.GetVariableView(Index.Zero);
        }

        /// <summary>
        /// Returns a variable view to the given element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="view">The view.</param>
        /// <param name="element">The element index.</param>
        /// <returns>The resolved variable view.</returns>
        public static VariableView<T> GetVariableView<T>(this ArrayView<T> view, Index element)
            where T : struct
        {
            return new VariableView<T>(view.GetSubView(element, 1));
        }

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
        public ArrayView2D(ArrayView<T> view, int height)
            : this(view, new Index2(view.Length / height, height))
        { }

        /// <summary>
        /// Constructs a new 2D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        public ArrayView2D(ArrayView<T> view, int width, int height)
            : this(view, new Index2(width, height))
        { }

        /// <summary>
        /// Constructs a new 2D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="extent">The extent (width, height) (number of elements).</param>
        public ArrayView2D(ArrayView<T> view, Index2 extent)
            : this(new ArrayView<T, Index2>(view, extent))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Width of this view.
        /// </summary>
        public int Width => Extent.X;

        /// <summary>
        /// Returns the height of this view.
        /// </summary>
        public int Height => Extent.Y;

        /// <summary>
        /// Returns the rows of this view that represents
        /// an implicitly transposed matrix.
        /// </summary>
        public int Rows => Extent.X;

        /// <summary>
        /// Returns the columns of this view that represents
        /// an implicitly transposed matrix.
        /// </summary>
        public int Columns => Extent.Y;

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        /// <returns>The element at the given index.</returns>
        [SuppressMessage("Microsoft.Design", "CA1023:IndexersShouldNotBeMultidimensional")]
        public ref T this[int x, int y] => ref BaseView[new Index2(x, y)];

        #endregion

        #region Methods

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <returns>A linear view to a single row.</returns>
        public ArrayView<T> GetRowView(int y)
        {
            Debug.Assert(y >= 0 && y < Height, "y out of bounds");
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
        public ArrayView3D(ArrayView<T> view, int height, int depth)
            : this(view, new Index3(view.Length / (height * depth), height, depth))
        { }

        /// <summary>
        /// Constructs a new 3D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        public ArrayView3D(ArrayView<T> view, int width, int height, int depth)
            : this(view, new Index3(width, height, depth))
        { }

        /// <summary>
        /// Constructs a new 3D array view.
        /// </summary>
        /// <param name="view">The linear view to the data.</param>
        /// <param name="extent">The extent (width, height, depth) (number of elements).</param>
        public ArrayView3D(ArrayView<T> view, Index3 extent)
            : this(new ArrayView<T, Index3>(view, extent))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the Width of this view.
        /// </summary>
        public int Width => Extent.X;

        /// <summary>
        /// Returns the height of this view.
        /// </summary>
        public int Height => Extent.Y;

        /// <summary>
        /// Returns the depth of this view.
        /// </summary>
        public int Depth => Extent.Z;

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        /// <param name="z">The z index.</param>
        /// <returns>The element at the given index.</returns>
        [SuppressMessage("Microsoft.Design", "CA1023:IndexersShouldNotBeMultidimensional")]
        public ref T this[int x, int y, int z] => ref BaseView[new Index3(x, y, z)];

        #endregion

        #region Methods

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A linear view to a single row.</returns>
        public ArrayView<T> GetRowView(int y, int z)
        {
            return GetSliceView(z).GetRowView(y);
        }

        /// <summary>
        /// Returns a 2D view to a single slice.
        /// </summary>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A 2D view to a single slice.</returns>
        public ArrayView2D<T> GetSliceView(int z)
        {
            Debug.Assert(z >= 0 && z < Depth, "z out of bounds");
            return new ArrayView2D<T>(
                BaseView.AsLinearView().GetSubView(z * Width * Height, Width * Height),
                Height);
        }

        #endregion
    }
}
