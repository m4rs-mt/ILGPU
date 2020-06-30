// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MemoryBufferExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
        ArrayView<T, Index1> IMemoryBuffer<T, Index1>.View => View;

        #endregion

        #region Methods

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(int height) =>
            View.As2DView(height);

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(int width, int height) =>
            View.As2DView(width, height);

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(Index2 extent) =>
            View.As2DView(extent);

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(int height, int depth) =>
            View.As3DView(height, depth);

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(int width, int height, int depth) =>
            View.As3DView(width, height, depth);

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(Index3 extent) =>
            View.As3DView(extent);

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
        /// <remarks>
        /// Note that the input array will stored as a transposed array to match the
        /// target layout.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "source")]
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
        /// <remarks>
        /// Note that the input array will stored as a transposed array to match the
        /// target layout.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "source")]
        public void CopyFrom(
            AcceleratorStream stream,
            T[,] source,
            Index2 sourceOffset,
            Index2 targetOffset,
            Index2 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 ||
                sourceOffset.X >= source.GetLength(0) ||
                sourceOffset.Y >= source.GetLength(1))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (targetOffset.X < 0 || targetOffset.Y < 0 ||
                targetOffset.X >= Extent.X ||
                targetOffset.Y >= Extent.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 ||
                sourceOffset.X + extent.X > source.GetLength(0) ||
                sourceOffset.Y + extent.Y > source.GetLength(1) ||
                targetOffset.X + extent.X > Extent.X ||
                targetOffset.Y + extent.Y > Extent.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];

            for (int i = 0; i < extent.X; ++i)
            {
                for (int j = 0; j < extent.Y; ++j)
                {
                    var targetIdx = new Index2(i, j).ComputeLinearIndex(extent);
                    tempBuffer[targetIdx] =
                        source[i + sourceOffset.X, j + sourceOffset.Y];
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
        /// Copies the contents to this buffer from the given array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>
        /// Note that the input array will stored as a transposed array to match the
        /// target layout.
        /// </remarks>
        [CLSCompliant(false)]
        public void CopyFrom(
            T[][] source,
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
        /// Copies the contents to this buffer from the given jagged array.
        /// Note that child arrays that are not initialized or do not have the
        /// appropriate length specified by <paramref name="extent"/> will be skipped
        /// and the values will have their default value.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>
        /// Note that the input array will stored as a transposed array to match the
        /// target layout.
        /// </remarks>
        [CLSCompliant(false)]
        public void CopyFrom(
            AcceleratorStream stream,
            T[][] source,
            Index2 sourceOffset,
            Index2 targetOffset,
            Index2 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 ||
                sourceOffset.X >= source.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (targetOffset.X < 0 || targetOffset.Y < 0 ||
                targetOffset.X >= Extent.X ||
                targetOffset.Y >= Extent.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 ||
                sourceOffset.X + extent.X > source.Length ||
                targetOffset.X + extent.X > Extent.X ||
                targetOffset.Y + extent.Y > Extent.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];

            for (int i = 0; i < extent.X; ++i)
            {
                var subData = source[i + sourceOffset.X];
                if (subData == null)
                    continue;

                // Skip entries that are out of bounds
                for (
                    int j = 0, e = IntrinsicMath.Min(subData.Length, extent.Y);
                    j < e;
                    ++j)
                {
                    var targetIdx = new Index2(i, j).ComputeLinearIndex(extent);
                    tempBuffer[targetIdx] = subData[j + sourceOffset.Y];
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
        /// <remarks>
        /// Note that the output array will contain the data as a transposed array to
        /// match the source layout.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
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
        /// <remarks>
        /// Note that the output array will contain the data as a transposed array to
        /// match the source layout.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public void CopyTo(
            AcceleratorStream stream,
            T[,] target,
            Index2 sourceOffset,
            Index2 targetOffset,
            Index2 extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 ||
                sourceOffset.X >= Extent.X ||
                sourceOffset.Y >= Extent.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (targetOffset.X < 0 || targetOffset.Y < 0 ||
                targetOffset.X >= target.GetLength(0) ||
                targetOffset.Y >= target.GetLength(1))
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 ||
                sourceOffset.X + extent.X > Extent.X ||
                sourceOffset.Y + extent.Y > Extent.Y ||
                targetOffset.X + extent.X > target.GetLength(0) ||
                targetOffset.Y + extent.Y > target.GetLength(1))
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];
            buffer.CopyTo(stream, tempBuffer, sourceOffset, 0, extent);

            for (int i = 0; i < extent.X; ++i)
            {
                for (int j = 0; j < extent.Y; ++j)
                {
                    var sourceIdx = new Index2(i, j).ComputeLinearIndex(extent);
                    target[i + targetOffset.X, j + targetOffset.Y] =
                        tempBuffer[sourceIdx];
                }
            }
        }

        /// <summary>
        /// Copies the contents of this buffer to the given jagged array using
        /// the default accelerator stream.
        /// Note that child arrays that are not initialized will be skipped during the
        /// copy operation.
        /// </summary>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>
        /// Note that the output array will contain the data as a transposed array to
        /// match the source layout.
        /// </remarks>
        [CLSCompliant(false)]
        public void CopyTo(
            T[][] target,
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
        /// Copies the contents of this buffer to the given jagged array.
        /// Note that child arrays that are not initialized will be skipped during the
        /// copy operation.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>
        /// Note that the output array will contain the data as a transposed array to
        /// match the source layout.
        /// </remarks>
        [CLSCompliant(false)]
        public void CopyTo(
            AcceleratorStream stream,
            T[][] target,
            Index2 sourceOffset,
            Index2 targetOffset,
            Index2 extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 ||
                sourceOffset.X >= Extent.X ||
                sourceOffset.Y >= Extent.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (targetOffset.X < 0 || targetOffset.Y < 0 ||
                targetOffset.X >= target.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 ||
                sourceOffset.X + extent.X > Extent.X ||
                sourceOffset.Y + extent.Y > Extent.Y ||
                targetOffset.X + extent.X > target.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];
            buffer.CopyTo(stream, tempBuffer, sourceOffset, 0, extent);

            for (int i = 0; i < extent.X; ++i)
            {
                var subData = target[i + targetOffset.X];
                if (subData == null)
                    continue;

                for (int j = 0; j < extent.Y; ++j)
                {
                    var sourceIdx = new Index2(i, j).ComputeLinearIndex(extent);
                    subData[j + targetOffset.Y] = tempBuffer[sourceIdx];
                }
            }
        }

        /// <summary>
        /// Copies the current contents into a new 2D array using
        /// the default accelerator stream.
        /// </summary>
        /// <returns>A new array holding the requested contents.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        public T[,] GetAs2DArray() => GetAs2DArray(Accelerator.DefaultStream);

        /// <summary>
        /// Copies the current contents into a new 2D array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        public T[,] GetAs2DArray(AcceleratorStream stream) =>
            GetAs2DArray(stream, default, Extent);

        /// <summary>
        /// Copies the current contents into a new 2D array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        public T[,] GetAs2DArray(Index2 offset, Index2 extent) =>
            GetAs2DArray(Accelerator.DefaultStream, offset, extent);

        /// <summary>
        /// Copies the current contents into a new 2D array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        public T[,] GetAs2DArray(AcceleratorStream stream, Index2 offset, Index2 extent)
        {
            if (extent.X < 1 || extent.Y < 1)
                throw new ArgumentOutOfRangeException(nameof(extent));

            var result = new T[extent.X, extent.Y];
            CopyTo(stream, result, offset, Index2.Zero, extent);
            return result;
        }

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <returns>A linear view to a single row.</returns>
        public ArrayView<T> GetRowView(int y) => View.GetRowView(y);

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        public ArrayView<T> AsLinearView() => View.AsLinearView();

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
        /// <remarks>
        /// Note that the input array will stored as a transposed array to match the
        /// target layout.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "source")]
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
        /// <remarks>
        /// Note that the input array will stored as a transposed array to match the
        /// target layout.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "source")]
        public void CopyFrom(
            AcceleratorStream stream,
            T[,,] source,
            Index3 sourceOffset,
            Index3 targetOffset,
            Index3 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 || sourceOffset.Z < 0 ||
                sourceOffset.X >= source.GetLength(0) ||
                sourceOffset.Y >= source.GetLength(1) ||
                sourceOffset.Z >= source.GetLength(2))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (targetOffset.X < 0 || targetOffset.Y < 0 || targetOffset.Z < 0 ||
                targetOffset.X >= Extent.X ||
                targetOffset.Y >= Extent.Y ||
                targetOffset.Z >= Extent.Z)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0 ||
                sourceOffset.X + extent.X > source.GetLength(0) ||
                sourceOffset.Y + extent.Y > source.GetLength(1) ||
                sourceOffset.Z + extent.Z > source.GetLength(2) ||
                targetOffset.X + extent.X > Extent.X ||
                targetOffset.Y + extent.Y > Extent.Y ||
                targetOffset.Z + extent.Z > Extent.Z)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

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
        /// Copies the contents of this buffer from the given jagged array using the
        /// default accelerator stream.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>
        /// Note that the input array will stored as a transposed array to match the
        /// target layout.
        /// </remarks>
        [CLSCompliant(false)]
        public void CopyFrom(
            T[][][] source,
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
        /// Copies the contents of this buffer from the given jagged array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="source">The source array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>
        /// Note that the input array will stored as a transposed array to match the
        /// target layout.
        /// </remarks>
        [CLSCompliant(false)]
        public void CopyFrom(
            AcceleratorStream stream,
            T[][][] source,
            Index3 sourceOffset,
            Index3 targetOffset,
            Index3 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 || sourceOffset.Z < 0 ||
                sourceOffset.X >= source.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (targetOffset.X < 0 || targetOffset.Y < 0 || targetOffset.Z < 0 ||
                targetOffset.X >= Extent.X ||
                targetOffset.Y >= Extent.Y ||
                targetOffset.Z >= Extent.Z)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0 ||
                sourceOffset.X + extent.X > source.Length ||
                targetOffset.X + extent.X > Extent.X ||
                targetOffset.Y + extent.Y > Extent.Y ||
                targetOffset.Z + extent.Z > Extent.Z)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];

            for (int i = 0; i < extent.X; ++i)
            {
                var subData = source[i + sourceOffset.X];
                if (subData == null)
                    continue;

                for (int j = 0; j < extent.Y; ++j)
                {
                    var subSubData = subData[j + sourceOffset.Y];
                    if (subSubData == null)
                        continue;

                    // Skip entries that are out of bounds
                    for (
                        int k = 0, e = IntrinsicMath.Min(subSubData.Length, extent.Z);
                        k < e;
                        ++k)
                    {
                        var targetIdx = new Index3(i, j, k).ComputeLinearIndex(extent);
                        tempBuffer[targetIdx] = subSubData[k + sourceOffset.Z];
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
        /// <remarks>
        /// Note that the output array will contain the data as a transposed array to
        /// match the source layout.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
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
        /// <remarks>
        /// Note that the output array will contain the data as a transposed array to
        /// match the source layout.
        /// </remarks>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional",
            Target = "target")]
        public void CopyTo(
            AcceleratorStream stream,
            T[,,] target,
            Index3 sourceOffset,
            Index3 targetOffset,
            Index3 extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 || sourceOffset.Z < 0 ||
                sourceOffset.X >= Extent.X ||
                sourceOffset.Y >= Extent.Y ||
                sourceOffset.Z >= Extent.Z)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (targetOffset.X < 0 || targetOffset.Y < 0 || targetOffset.Z < 0 ||
                targetOffset.X >= target.GetLength(0) ||
                targetOffset.Y >= target.GetLength(1) ||
                targetOffset.Z >= target.GetLength(2))
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0 ||
                sourceOffset.X + extent.X > Extent.X ||
                sourceOffset.Y + extent.Y > Extent.Y ||
                sourceOffset.Z + extent.Z > Extent.Z ||
                targetOffset.X + extent.X > target.GetLength(0) ||
                targetOffset.Y + extent.Y > target.GetLength(1) ||
                targetOffset.Z + extent.Z > target.GetLength(2))
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

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
        /// Copies the contents of this buffer to the given jagged array using the
        /// default accelerator stream.
        /// Note that child arrays that are not initialized will be skipped during the
        /// copy operation.
        /// </summary>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>
        /// Note that the output array will contain the data as a transposed array to
        /// match the source layout.
        /// </remarks>
        [CLSCompliant(false)]
        public void CopyTo(
            T[][][] target,
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
        /// Copies the contents of this buffer to the given jagged array.
        /// Note that child arrays that are not initialized will be skipped during the
        /// copy operation.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="target">The target array.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="targetOffset">The target offset.</param>
        /// <param name="extent">The length.</param>
        /// <remarks>
        /// Note that the output array will contain the data as a transposed array to
        /// match the source layout.
        /// </remarks>
        [CLSCompliant(false)]
        public void CopyTo(
            AcceleratorStream stream,
            T[][][] target,
            Index3 sourceOffset,
            Index3 targetOffset,
            Index3 extent)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 || sourceOffset.Z < 0 ||
                sourceOffset.X >= Extent.X ||
                sourceOffset.Y >= Extent.Y ||
                sourceOffset.Z >= Extent.Z)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOffset));
            }

            if (targetOffset.X < 0 || targetOffset.Y < 0 || targetOffset.Z < 0 ||
                targetOffset.X >= target.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0 ||
                sourceOffset.X + extent.X > Extent.X ||
                sourceOffset.Y + extent.Y > Extent.Y ||
                sourceOffset.Z + extent.Z > Extent.Z ||
                targetOffset.X + extent.X > target.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];
            buffer.CopyTo(
                stream,
                tempBuffer,
                sourceOffset,
                0,
                extent);

            for (int i = 0; i < extent.X; ++i)
            {
                var subData = target[i + targetOffset.X];
                if (subData == null)
                    continue;
                for (int j = 0; j < extent.Y; ++j)
                {
                    var subSubData = subData[j + targetOffset.Y];
                    if (subSubData == null)
                        continue;
                    for (int k = 0; k < extent.Z; ++k)
                    {
                        var sourceIdx = new Index3(i, j, k).ComputeLinearIndex(extent);
                        subSubData[k + targetOffset.Z] = tempBuffer[sourceIdx];
                    }
                }
            }
        }

        /// <summary>
        /// Copies the current contents into a new 3D array using
        /// the default accelerator stream.
        /// </summary>
        /// <returns>A new array holding the requested contents.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        public T[,,] GetAs3DArray() => GetAs3DArray(Accelerator.DefaultStream);

        /// <summary>
        /// Copies the current contents into a new 2D array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <returns>A new array holding the requested contents.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        public T[,,] GetAs3DArray(AcceleratorStream stream) =>
            GetAs3DArray(stream, default, Extent);

        /// <summary>
        /// Copies the current contents into a new 2D array using
        /// the default accelerator stream.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        public T[,,] GetAs3DArray(Index3 offset, Index3 extent) =>
            GetAs3DArray(Accelerator.DefaultStream, offset, extent);

        /// <summary>
        /// Copies the current contents into a new 2D array.
        /// </summary>
        /// <param name="stream">The used accelerator stream.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="extent">The extent (number of elements).</param>
        /// <returns>A new array holding the requested contents.</returns>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        public T[,,] GetAs3DArray(AcceleratorStream stream, Index3 offset, Index3 extent)
        {
            if (extent.X < 1 || extent.Y < 1 || extent.Z < 1)
                throw new ArgumentOutOfRangeException(nameof(extent));

            var result = new T[extent.X, extent.Y, extent.Z];
            CopyTo(stream, result, offset, Index3.Zero, extent);
            return result;
        }

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A linear view to a single row.</returns>
        public ArrayView<T> GetRowView(int y, int z) => View.GetRowView(y, z);

        /// <summary>
        /// Returns a 2D view to a single slice.
        /// </summary>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A 2D view to a single slice.</returns>
        public ArrayView2D<T> GetSliceView(int z) => View.GetSliceView(z);

        /// <summary>
        /// Converts the current view into a linear view.
        /// </summary>
        /// <returns>The converted linear view.</returns>
        public ArrayView<T> AsLinearView() => View.AsLinearView();

        #endregion
    }

    /// <summary>
    /// Extension methods for the allocation of memory buffers.
    /// </summary>
    public static partial class MemoryBufferExtensions { }
}
