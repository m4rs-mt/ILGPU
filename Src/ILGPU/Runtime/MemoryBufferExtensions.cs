// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: MemoryBufferExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime
{
    partial class MemoryBuffer<T>
    {
        #region Properties

        /// <summary>
        /// Returns an array view that can access this buffer.
        /// </summary>
        ArrayView<T, Index> IMemoryBuffer<T, Index>.View => View;

        #endregion

        #region Methods

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(int height)
        {
            return View.As2DView(height);
        }

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(int width, int height)
        {
            return View.As2DView(width, height);
        }

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(Index2 extent)
        {
            return View.As2DView(extent);
        }

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(int height, int depth)
        {
            return View.As3DView(height, depth);
        }

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(int width, int height, int depth)
        {
            return View.As3DView(width, height, depth);
        }

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(Index3 extent)
        {
            return View.As3DView(extent);
        }

        #endregion
    }

    partial class MemoryBuffer2D<T>
    {
        #region Properties

        /// <summary>
        /// Returns an array view that can access this buffer.
        /// </summary>
        ArrayView<T, Index2> IMemoryBuffer<T, Index2>.View => View.BaseView;

        /// <summary>
        /// Returns the width (x-dimension) of this buffer.
        /// </summary>
        public int Width => Extent.X;

        /// <summary>
        /// Returns the height (y-dimension) of this buffer.
        /// </summary>
        public int Height => Extent.Y;

        #endregion

        #region Methods

        /// <summary>
        /// Copies the contents to this buffer from the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>Note that the input array will stored as a transposed array to match the target layout.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1814: PreferJaggedArraysOverMultidimensional", Target = "source")]
        public void CopyFrom(
            T[,] source,
            Index2 sourceOffset,
            Index2 targetOffset,
            Index2 extent) =>
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
        /// <param name="extent">The length.</param>
        /// <remarks>Note that the input array will stored as a transposed array to match the target layout.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1814: PreferJaggedArraysOverMultidimensional", Target = "source")]
        public void CopyFrom(
            AcceleratorStream stream,
            T[,] source,
            Index2 sourceOffset,
            Index2 targetOffset,
            Index2 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (extent.X < 0 || extent.Y < 0 ||
                extent.X > source.GetLength(0) || extent.Y > source.GetLength(1))
                throw new ArgumentOutOfRangeException(nameof(extent));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 ||
                sourceOffset.X >= extent.X || sourceOffset.Y >= extent.Y)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));

            var tempBuffer = new T[extent.Size];

            for (int i = 0; i < extent.X; ++i)
            {
                for (int j = 0; j < extent.Y; ++j)
                {
                    var targetIdx = new Index2(i, j).ComputeLinearIndex(extent);
                    tempBuffer[targetIdx] = source[i + sourceOffset.X, j + sourceOffset.Y];
                }
            }

            buffer.CopyFrom(
                stream,
                tempBuffer,
                0,
                targetOffset,
                extent.Size);
        }

        /// <summary>
        /// Copies the contents of this buffer to the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>Note that the output array will contain the data as a transposed array to match the source layout.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1814: PreferJaggedArraysOverMultidimensional", Target = "target")]
        public void CopyTo(
            T[,] target,
            Index2 sourceOffset,
            Index2 targetOffset,
            Index2 extent) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies the contents of this buffer to the given array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>Note that the output array will contain the data as a transposed array to match the source layout.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1814: PreferJaggedArraysOverMultidimensional", Target = "target")]
        public void CopyTo(
            AcceleratorStream stream,
            T[,] target,
            Index2 sourceOffset,
            Index2 targetOffset,
            Index2 extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (extent.X < 0 || extent.Y < 0 ||
                extent.X > target.GetLength(0) || extent.Y > target.GetLength(1))
                throw new ArgumentOutOfRangeException(nameof(extent));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 ||
                sourceOffset.X >= Extent.X || sourceOffset.Y >= Extent.Y)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));

            var tempBuffer = new T[extent.Size];
            buffer.CopyTo(stream, tempBuffer, sourceOffset, 0, extent);

            for (int i = 0; i < extent.X; ++i)
            {
                for (int j = 0; j < extent.Y; ++j)
                {
                    var sourceIdx = new Index2(i, j).ComputeLinearIndex(extent);
                    target[i + targetOffset.X, j + targetOffset.Y] = tempBuffer[sourceIdx];
                }
            }
        }

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <returns>A linear view to a single row.</returns>
        public ArrayView<T> GetRowView(int y)
        {
            return View.GetRowView(y);
        }

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        public ArrayView<T> AsLinearView()
        {
            return View.AsLinearView();
        }

        #endregion
    }

    partial class MemoryBuffer3D<T>
    {
        #region Properties

        /// <summary>
        /// Returns an array view that can access this buffer.
        /// </summary>
        ArrayView<T, Index3> IMemoryBuffer<T, Index3>.View => View.BaseView;

        /// <summary>
        /// Returns the width (x-dimension) of this buffer.
        /// </summary>
        public int Width => Extent.X;

        /// <summary>
        /// Returns the height (y-dimension) of this buffer.
        /// </summary>
        public int Height => Extent.Y;

        /// <summary>
        /// Returns the depth (z-dimension) of this buffer.
        /// </summary>
        public int Depth => Extent.Z;

        #endregion

        #region Methods

        /// <summary>
        /// Copies the contents of this buffer from the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>Note that the input array will stored as a transposed array to match the target layout.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1814: PreferJaggedArraysOverMultidimensional", Target = "source")]
        public void CopyFrom(
            T[,,] source,
            Index3 sourceOffset,
            Index3 targetOffset,
            Index3 extent) =>
            CopyFrom(
                Accelerator.DefaultStream,
                source,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies the contents of this buffer from the given array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>Note that the input array will stored as a transposed array to match the target layout.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1814: PreferJaggedArraysOverMultidimensional", Target = "source")]
        public void CopyFrom(
            AcceleratorStream stream,
            T[,,] source,
            Index3 sourceOffset,
            Index3 targetOffset,
            Index3 extent)
        {
            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0 ||
                extent.X > source.GetLength(0) ||
                extent.Y > source.GetLength(1) ||
                extent.Z > source.GetLength(2))
                throw new ArgumentOutOfRangeException(nameof(extent));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 || sourceOffset.Z < 0 ||
                sourceOffset.X >= extent.X ||
                sourceOffset.Y >= extent.Y ||
                sourceOffset.Z >= extent.Z)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));

            var tempBuffer = new T[extent.Size];

            for (int i = 0; i < extent.X; ++i)
            {
                for (int j = 0; j < extent.Y; ++j)
                {
                    for (int k = 0; k < extent.Z; ++k)
                    {
                        var targetIdx = new Index3(i, j, k).ComputeLinearIndex(extent);
                        tempBuffer[targetIdx] = source[
                            i + sourceOffset.X,
                            j + sourceOffset.Y,
                            k + sourceOffset.Z];
                    }
                }
            }

            buffer.CopyFrom(
                stream,
                tempBuffer,
                0,
                targetOffset,
                extent.Size);
        }

        /// <summary>
        /// Copies the contents to this buffer to the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>Note that the output array will contain the data as a transposed array to match the source layout.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1814: PreferJaggedArraysOverMultidimensional", Target = "target")]
        public void CopyTo(
            T[,,] target,
            Index3 sourceOffset,
            Index3 targetOffset,
            Index3 extent) =>
            CopyTo(
                Accelerator.DefaultStream,
                target,
                sourceOffset,
                targetOffset,
                extent);

        /// <summary>
        /// Copies the contents to this buffer to the given array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>Note that the output array will contain the data as a transposed array to match the source layout.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1814: PreferJaggedArraysOverMultidimensional", Target = "target")]
        public void CopyTo(
            AcceleratorStream stream,
            T[,,] target,
            Index3 sourceOffset,
            Index3 targetOffset,
            Index3 extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0 ||
                extent.X > target.GetLength(0) ||
                extent.Y > target.GetLength(1) ||
                extent.Z > target.GetLength(2))
                throw new ArgumentOutOfRangeException(nameof(extent));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 || sourceOffset.Z < 0 ||
                sourceOffset.X >= Extent.X ||
                sourceOffset.Y >= Extent.Y ||
                sourceOffset.Z >= Extent.Z)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));

            var tempBuffer = new T[extent.Size];
            buffer.CopyTo(stream, tempBuffer, sourceOffset, 0, extent);

            for (int i = 0; i < extent.X; ++i)
            {
                for (int j = 0; j < extent.Y; ++j)
                {
                    for (int k = 0; k < extent.Z; ++k)
                    {
                        var sourceIdx = new Index3(i, j, k).ComputeLinearIndex(extent);
                        target[
                            i + targetOffset.X,
                            j + targetOffset.Y,
                            k + targetOffset.Z] = tempBuffer[sourceIdx];
                    }
                }
            }
        }

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A linear view to a single row.</returns>
        public ArrayView<T> GetRowView(int y, int z)
        {
            return View.GetRowView(y, z);
        }

        /// <summary>
        /// Returns a 2D view to a single slice.
        /// </summary>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A 2D view to a single slice.</returns>
        public ArrayView2D<T> GetSliceView(int z)
        {
            return View.GetSliceView(z);
        }

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        public ArrayView<T> AsLinearView()
        {
            return View.AsLinearView();
        }

        #endregion
    }
}
