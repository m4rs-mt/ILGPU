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

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU
{
    partial struct ArrayView<T>
    {
        #region Instance

        /// <summary>
        /// Constructs a new 1D array view.
        /// </summary>
        /// <param name="data">The data pointer to the first element.</param>
        /// <param name="extent">The extent (number of elements).</param>
        public ArrayView(IntPtr data, int extent)
            : this(data, new Index(extent))
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Access the element at the given index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element at the given index.</returns>
        public T this[int index]
        {
            get { return Load(index); }
            set { Store(index, value); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The converted 2D view.</returns>
        public ArrayView2D<T> As2DView(int height)
        {
            return new ArrayView2D<T>(this, height);
        }

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The converted 2D view.</returns>
        public ArrayView2D<T> As2DView(int width, int height)
        {
            return new ArrayView2D<T>(this, width, height);
        }

        /// <summary>
        /// Converts this view into a new 2D view.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns>The converted 2D view.</returns>
        public ArrayView2D<T> As2DView(Index2 extent)
        {
            return new ArrayView2D<T>(this, extent);
        }

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The converted 3D view.</returns>
        public ArrayView3D<T> As3DView(int height, int depth)
        {
            return new ArrayView3D<T>(this, height, depth);
        }

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The converted 3D view.</returns>
        public ArrayView3D<T> As3DView(int width, int height, int depth)
        {
            return new ArrayView3D<T>(this, width, height, depth);
        }

        /// <summary>
        /// Converts this view into a new 3D view.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns>The converted 3D view.</returns>
        public ArrayView3D<T> As3DView(Index3 extent)
        {
            return new ArrayView3D<T>(this, extent);
        }

        #endregion
    }

    partial struct ArrayView2D<T>
    {
        #region Instance

        /// <summary>
        /// Constructs a new 2D array view.
        /// </summary>
        /// <param name="data">The data pointer to the first element.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        public ArrayView2D(IntPtr data, int width, int height)
            : this(data, new Index2(width, height))
        { }

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
        {
            Debug.Assert(extent.X > 0, "Width of of range");
            Debug.Assert(extent.Y > 0, "Height of of range");
            Debug.Assert(view.Length >= extent.Size, "Extent out of range");
            this.view = new ArrayView<T, Index2>(view.Pointer, extent);
        }

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
        public T this[int x, int y]
        {
            get { return Load(new Index2(x, y)); }
            set { Store(new Index2(x, y), value); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a variable view for the element at the given index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        /// <returns>A variable view for the element at the given index.</returns>
        public VariableView<T> GetVariableView(int x, int y)
        {
            return GetVariableView(new Index2(x, y));
        }

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

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        public ArrayView<T> AsLinearView()
        {
            return new ArrayView<T>(view.AsLinearView());
        }

        #endregion
    }

    partial struct ArrayView3D<T>
    {
        #region Instance

        /// <summary>
        /// Constructs a new 3D array view.
        /// </summary>
        /// <param name="data">The data pointer to the first element.</param>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        public ArrayView3D(IntPtr data, int width, int height, int depth)
            : this(data, new Index3(width, height, depth))
        { }

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
        {
            Debug.Assert(extent.X > 0, "Width of of range");
            Debug.Assert(extent.Y > 0, "Height of of range");
            Debug.Assert(extent.Z > 0, "Depth of of range");
            Debug.Assert(view.Length >= extent.Size, "Extent out of range");
            this.view = new ArrayView<T, Index3>(view.Pointer, extent);
        }

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
        public T this[int x, int y, int z]
        {
            get { return Load(new Index3(x, y, z)); }
            set { Store(new Index3(x, y, z), value); }
        }

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
                view.AsLinearView().GetSubView(z * Width * Height,
                Width * Height),
                Height);
        }

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        public ArrayView<T> AsLinearView()
        {
            return new ArrayView<T>(view.AsLinearView());
        }

        #endregion
    }
}
