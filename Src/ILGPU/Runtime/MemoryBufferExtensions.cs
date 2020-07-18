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
        #region Methods

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(long height) =>
            View.As2DView(height);

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(long width, long height) =>
            View.As2DView(width, height);

        /// <summary>
        /// Returns a 2D view to this linear buffer.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns>The 2D view.</returns>
        public ArrayView2D<T> As2DView(LongIndex2 extent) =>
            View.As2DView(extent);

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(long height, long depth) =>
            View.As3DView(height, depth);

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="width">The width (number of elements in x direction).</param>
        /// <param name="height">The height (number of elements in y direction).</param>
        /// <param name="depth">The depth (number of elements in z direction).</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(long width, long height, int depth) =>
            View.As3DView(width, height, depth);

        /// <summary>
        /// Returns a 3D view to this linear buffer.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <returns>The 3D view.</returns>
        public ArrayView3D<T> As3DView(LongIndex3 extent) =>
            View.As3DView(extent);

        #endregion
    }

    partial class MemoryBuffer2D<T>
    {
        #region Properties

        /// <summary>
        /// Returns the width (x-dimension) of this buffer.
        /// </summary>
        public long Width => Extent.X;

        /// <summary>
        /// Returns the height (y-dimension) of this buffer.
        /// </summary>
        public long Height => Extent.Y;

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
            LongIndex2 sourceOffset,
            LongIndex2 targetOffset,
            LongIndex2 extent) =>
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
            LongIndex2 sourceOffset,
            LongIndex2 targetOffset,
            LongIndex2 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 ||
                sourceOffset.X >= source.GetLongLength(0) ||
                sourceOffset.Y >= source.GetLongLength(1))
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
                sourceOffset.X + extent.X > source.GetLongLength(0) ||
                sourceOffset.Y + extent.Y > source.GetLongLength(1) ||
                targetOffset.X + extent.X > Extent.X ||
                targetOffset.Y + extent.Y > Extent.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];

            for (long i = 0; i < extent.X; ++i)
            {
                for (long j = 0; j < extent.Y; ++j)
                {
                    var targetIdx = new LongIndex2(i, j).
                        ComputeLinearIndex(extent);
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
            LongIndex2 sourceOffset,
            LongIndex2 targetOffset,
            LongIndex2 extent) =>
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
            LongIndex2 sourceOffset,
            LongIndex2 targetOffset,
            LongIndex2 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 ||
                sourceOffset.X >= source.LongLength)
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
                sourceOffset.X + extent.X > source.LongLength ||
                targetOffset.X + extent.X > Extent.X ||
                targetOffset.Y + extent.Y > Extent.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];

            for (long i = 0; i < extent.X; ++i)
            {
                var subData = source[i + sourceOffset.X];
                if (subData == null)
                    continue;

                // Skip entries that are out of bounds
                for (
                    long j = 0, e = IntrinsicMath.Min(subData.LongLength, extent.Y);
                    j < e;
                    ++j)
                {
                    var targetIdx = new LongIndex2(i, j).
                        ComputeLinearIndex(extent);
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
            LongIndex2 sourceOffset,
            LongIndex2 targetOffset,
            LongIndex2 extent) =>
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
            LongIndex2 sourceOffset,
            LongIndex2 targetOffset,
            LongIndex2 extent)
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
                targetOffset.X >= target.GetLongLength(0) ||
                targetOffset.Y >= target.GetLongLength(1))
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 ||
                sourceOffset.X + extent.X > Extent.X ||
                sourceOffset.Y + extent.Y > Extent.Y ||
                targetOffset.X + extent.X > target.GetLongLength(0) ||
                targetOffset.Y + extent.Y > target.GetLongLength(1))
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];
            buffer.CopyTo(stream, tempBuffer, sourceOffset, 0, extent);

            for (long i = 0; i < extent.X; ++i)
            {
                for (long j = 0; j < extent.Y; ++j)
                {
                    var sourceIdx = new LongIndex2(i, j).
                        ComputeLinearIndex(extent);
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
            LongIndex2 sourceOffset,
            LongIndex2 targetOffset,
            LongIndex2 extent) =>
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
            LongIndex2 sourceOffset,
            LongIndex2 targetOffset,
            LongIndex2 extent)
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
                targetOffset.X >= target.LongLength)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 ||
                sourceOffset.X + extent.X > Extent.X ||
                sourceOffset.Y + extent.Y > Extent.Y ||
                targetOffset.X + extent.X > target.LongLength)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];
            buffer.CopyTo(stream, tempBuffer, sourceOffset, 0, extent);

            for (long i = 0; i < extent.X; ++i)
            {
                var subData = target[i + targetOffset.X];
                if (subData == null)
                    continue;

                for (long j = 0; j < extent.Y; ++j)
                {
                    var sourceIdx = new LongIndex2(i, j).
                        ComputeLinearIndex(extent);
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
        public T[,] GetAs2DArray(LongIndex2 offset, LongIndex2 extent) =>
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
        public T[,] GetAs2DArray(
            AcceleratorStream stream,
            LongIndex2 offset,
            LongIndex2 extent)
        {
            if (extent.X < 1 || extent.Y < 1)
                throw new ArgumentOutOfRangeException(nameof(extent));

            var result = new T[extent.X, extent.Y];
            CopyTo(stream, result, offset, LongIndex2.Zero, extent);
            return result;
        }

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <returns>A linear view to a single row.</returns>
        public ArrayView<T> GetRowView(long y) => View.GetRowView(y);

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
        /// Returns the width (x-dimension) of this buffer.
        /// </summary>
        public long Width => Extent.X;

        /// <summary>
        /// Returns the height (y-dimension) of this buffer.
        /// </summary>
        public long Height => Extent.Y;

        /// <summary>
        /// Returns the depth (z-dimension) of this buffer.
        /// </summary>
        public long Depth => Extent.Z;

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
            LongIndex3 sourceOffset,
            LongIndex3 targetOffset,
            LongIndex3 extent) =>
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
            LongIndex3 sourceOffset,
            LongIndex3 targetOffset,
            LongIndex3 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 || sourceOffset.Z < 0 ||
                sourceOffset.X >= source.GetLongLength(0) ||
                sourceOffset.Y >= source.GetLongLength(1) ||
                sourceOffset.Z >= source.GetLongLength(2))
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

            for (long i = 0; i < extent.X; ++i)
            {
                for (long j = 0; j < extent.Y; ++j)
                {
                    for (long k = 0; k < extent.Z; ++k)
                    {
                        var targetIdx = new LongIndex3(i, j, k).
                            ComputeLinearIndex(extent);
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
            LongIndex3 sourceOffset,
            LongIndex3 targetOffset,
            LongIndex3 extent) =>
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
            LongIndex3 sourceOffset,
            LongIndex3 targetOffset,
            LongIndex3 extent)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sourceOffset.X < 0 || sourceOffset.Y < 0 || sourceOffset.Z < 0 ||
                sourceOffset.X >= source.LongLength)
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
                sourceOffset.X + extent.X > source.LongLength ||
                targetOffset.X + extent.X > Extent.X ||
                targetOffset.Y + extent.Y > Extent.Y ||
                targetOffset.Z + extent.Z > Extent.Z)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];

            for (long i = 0; i < extent.X; ++i)
            {
                var subData = source[i + sourceOffset.X];
                if (subData == null)
                    continue;

                for (long j = 0; j < extent.Y; ++j)
                {
                    var subSubData = subData[j + sourceOffset.Y];
                    if (subSubData == null)
                        continue;

                    // Skip entries that are out of bounds
                    for (
                        long k = 0, e = IntrinsicMath.Min(
                            subSubData.LongLength,
                            extent.Z);
                        k < e;
                        ++k)
                    {
                        var targetIdx = new LongIndex3(i, j, k).
                            ComputeLinearIndex(extent);
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
            LongIndex3 sourceOffset,
            LongIndex3 targetOffset,
            LongIndex3 extent) =>
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
            LongIndex3 sourceOffset,
            LongIndex3 targetOffset,
            LongIndex3 extent)
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
                targetOffset.X >= target.GetLongLength(0) ||
                targetOffset.Y >= target.GetLongLength(1) ||
                targetOffset.Z >= target.GetLongLength(2))
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0 ||
                sourceOffset.X + extent.X > Extent.X ||
                sourceOffset.Y + extent.Y > Extent.Y ||
                sourceOffset.Z + extent.Z > Extent.Z ||
                targetOffset.X + extent.X > target.GetLongLength(0) ||
                targetOffset.Y + extent.Y > target.GetLongLength(1) ||
                targetOffset.Z + extent.Z > target.GetLongLength(2))
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            var tempBuffer = new T[extent.Size];
            buffer.CopyTo(stream, tempBuffer, sourceOffset, 0, extent);

            for (long i = 0; i < extent.X; ++i)
            {
                for (long j = 0; j < extent.Y; ++j)
                {
                    for (long k = 0; k < extent.Z; ++k)
                    {
                        var sourceIdx = new LongIndex3(i, j, k).
                            ComputeLinearIndex(extent);
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
            LongIndex3 sourceOffset,
            LongIndex3 targetOffset,
            LongIndex3 extent) =>
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
            LongIndex3 sourceOffset,
            LongIndex3 targetOffset,
            LongIndex3 extent)
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
                targetOffset.X >= target.LongLength)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            }

            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0 ||
                sourceOffset.X + extent.X > Extent.X ||
                sourceOffset.Y + extent.Y > Extent.Y ||
                sourceOffset.Z + extent.Z > Extent.Z ||
                targetOffset.X + extent.X > target.LongLength)
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

            for (long i = 0; i < extent.X; ++i)
            {
                var subData = target[i + targetOffset.X];
                if (subData == null)
                    continue;
                for (long j = 0; j < extent.Y; ++j)
                {
                    var subSubData = subData[j + targetOffset.Y];
                    if (subSubData == null)
                        continue;
                    for (long k = 0; k < extent.Z; ++k)
                    {
                        var sourceIdx = new LongIndex3(i, j, k).
                            ComputeLinearIndex(extent);
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
        public T[,,] GetAs3DArray(LongIndex3 offset, LongIndex3 extent) =>
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
        public T[,,] GetAs3DArray(
            AcceleratorStream stream,
            LongIndex3 offset,
            LongIndex3 extent)
        {
            if (extent.X < 1 || extent.Y < 1 || extent.Z < 1)
                throw new ArgumentOutOfRangeException(nameof(extent));

            var result = new T[extent.X, extent.Y, extent.Z];
            CopyTo(stream, result, offset, LongIndex3.Zero, extent);
            return result;
        }

        /// <summary>
        /// Returns a linear view to a single row.
        /// </summary>
        /// <param name="y">The y index of the row.</param>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A linear view to a single row.</returns>
        public ArrayView<T> GetRowView(long y, long z) => View.GetRowView(y, z);

        /// <summary>
        /// Returns a 2D view to a single slice.
        /// </summary>
        /// <param name="z">The z index of the slice.</param>
        /// <returns>A 2D view to a single slice.</returns>
        public ArrayView2D<T> GetSliceView(long z) => View.GetSliceView(z);

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
