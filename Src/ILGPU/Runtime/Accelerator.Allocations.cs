// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Accelerator.Allocations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime
{
    partial class Accelerator
    {
        #region Allocations

        /// <summary>
        /// Allocates a buffer with the specified size in bytes on this accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="length">The size in bytes to allocate.</param>
        /// <returns>An allocated buffer on the this accelerator.</returns>
        public MemoryBuffer1D<T, Stride1D.Dense> Allocate1D<T>(long length)
            where T : unmanaged =>
            Allocate1D<T, Stride1D.Dense>(length, default);

        /// <summary>
        /// Allocates a 1D buffer with the specified number of elements on this
        /// accelerator.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The buffer stride type.</typeparam>
        /// <param name="length">The number of elements to allocate.</param>
        /// <param name="stride">The buffer stride to use.</param>
        /// <returns>An allocated 1D buffer on the this accelerator.</returns>
        public MemoryBuffer1D<T, TStride> Allocate1D<T, TStride>(
            long length,
            TStride stride)
            where T : unmanaged
            where TStride : struct, IStride1D
        {
            // Allocate the raw chunk of memory
            var baseView = AllocateRaw<T, LongIndex1D, Index1D, TStride>(length, stride);

            // Create the resulting memory buffer wrapper
            return new MemoryBuffer1D<T, TStride>(
                this,
                new ArrayView1D<T, TStride>(
                    baseView,
                    length,
                    stride));
        }

        /// <summary>
        /// Builds a new 2D stride based on a generic stride description.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The buffer stride type.</typeparam>
        /// <param name="extent">The number of elements to use.</param>
        /// <param name="getLeadingDimensionSize">
        /// Determines the size of the leading dimension.
        /// </param>
        /// <param name="buildStride">Builds a new stride.</param>
        /// <returns>The 2D stride.</returns>
        /// <remarks>
        /// The leading dimension must be less or equal to <see cref="int.MaxValue"/>.
        /// </remarks>
        private static TStride Build2DStride<T, TStride>(
            in LongIndex2D extent,
            Stride2D.GetLeadingDimensionSize getLeadingDimensionSize,
            Stride2D.BuildStride<TStride> buildStride)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            if (extent.X < 0 || extent.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(extent));
            if (getLeadingDimensionSize is null)
                throw new ArgumentNullException(nameof(getLeadingDimensionSize));
            if (buildStride is null)
                throw new ArgumentNullException(nameof(buildStride));

            // Get the leading dimension
            long leadingDimSize = getLeadingDimensionSize(extent);
            if (leadingDimSize < 0 || leadingDimSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(extent));

            // Build the stride
            return buildStride(extent, (int)leadingDimSize);
        }

        /// <summary>
        /// Allocates a 2D buffer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The buffer stride type.</typeparam>
        /// <param name="extent">The number of elements to use.</param>
        /// <param name="getLeadingDimensionSize">
        /// Determines the size of the leading dimension.
        /// </param>
        /// <param name="buildStride">Builds a new stride.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        /// <remarks>
        /// The leading dimension must be less or equal to <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer2D<T, TStride> Allocate2D<T, TStride>(
            in LongIndex2D extent,
            Stride2D.GetLeadingDimensionSize getLeadingDimensionSize,
            Stride2D.BuildStride<TStride> buildStride)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            // Build the stride
            var stride = Build2DStride<T, TStride>(
                extent,
                getLeadingDimensionSize,
                buildStride);

            // Allocate the raw chunk of memory
            var baseView = AllocateRaw<T, LongIndex2D, Index2D, TStride>(extent, stride);

            // Create the resulting memory buffer wrapper
            return new MemoryBuffer2D<T, TStride>(
                this,
                new ArrayView2D<T, TStride>(
                    baseView,
                    extent,
                    stride));
        }

        /// <summary>
        /// Allocates a 2D buffer with X being the leading dimension.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        /// <remarks>
        /// Since X is the leading dimension, X must be less or equal to
        /// <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer2D<T, Stride2D.DenseX> Allocate2DDenseX<T>(
            in LongIndex2D extent)
            where T : unmanaged =>
            Allocate2D<T, Stride2D.DenseX>(
                extent,
                ex => ex.X,
                (ex, leadingDim) => new Stride2D.DenseX(leadingDim));

        /// <summary>
        /// Allocates a 2D buffer with Y being the leading dimension.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        /// <remarks>
        /// Since Y is the leading dimension, Y must be less or equal to
        /// <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer2D<T, Stride2D.DenseY> Allocate2DDenseY<T>(
            in LongIndex2D extent)
            where T : unmanaged =>
            Allocate2D<T, Stride2D.DenseY>(
                extent,
                ex => ex.Y,
                (ex, leadingDim) => new Stride2D.DenseY(leadingDim));

        /// <summary>
        /// Allocates a pitched 2D buffer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The buffer stride type.</typeparam>
        /// <param name="extent">The number of elements to use.</param>
        /// <param name="getLeadingDimensionSize">
        /// Determines the size of the leading dimension.
        /// </param>
        /// <param name="buildStride">Builds a new stride.</param>
        /// <param name="alignmentInBytes">
        /// The alignment in bytes of the leading dimension.
        /// </param>
        /// <returns>An allocated pitched 2D buffer on the this accelerator.</returns>
        private MemoryBuffer2D<T, TStride> Allocate2DPitched<T, TStride>(
            in LongIndex2D extent,
            Stride2D.GetLeadingDimensionSize getLeadingDimensionSize,
            Stride2D.BuildStride<TStride> buildStride,
            int alignmentInBytes)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            // Determines a pitched leading dimension.
            long GetPitchedLeadingDimension(LongIndex2D extent)
            {
                long leadingDim = getLeadingDimensionSize(extent);
                return StrideExtensions.GetPitchedLeadingDimension<T>(
                    leadingDim,
                    alignmentInBytes);
            }

            return Allocate2D<T, TStride>(
                extent,
                GetPitchedLeadingDimension,
                buildStride);
        }

        /// <summary>
        /// Allocates a pitched 2D buffer with X being the leading dimension.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <param name="xAlignmentInBytes">
        /// The alignment in bytes of the leading dimension.
        /// </param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        /// <remarks>
        /// Since X is the leading dimension, X must be less or equal to
        /// <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer2D<T, Stride2D.DenseX> Allocate2DPitchedX<T>(
            in LongIndex2D extent,
            int xAlignmentInBytes)
            where T : unmanaged =>
            Allocate2DPitched<T, Stride2D.DenseX>(
                extent,
                ex => ex.X,
                (ex, leadingDim) => new Stride2D.DenseX(leadingDim),
                xAlignmentInBytes);

        /// <summary>
        /// Allocates a pitched 2D buffer with Y being the leading dimension.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <param name="yAlignmentInBytes">
        /// The alignment in bytes of the leading dimension.
        /// </param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        /// <remarks>
        /// Since Y is the leading dimension, Y must be less or equal to
        /// <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer2D<T, Stride2D.DenseY> Allocate2DPitchedY<T>(
            in LongIndex2D extent,
            int yAlignmentInBytes)
            where T : unmanaged =>
            Allocate2DPitched<T, Stride2D.DenseY>(
                extent,
                ex => ex.Y,
                (ex, leadingDim) => new Stride2D.DenseY(leadingDim),
                yAlignmentInBytes);

        /// <summary>
        /// Builds a new 3D stride based on a generic stride description.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The buffer stride type.</typeparam>
        /// <param name="extent">The number of elements to use.</param>
        /// <param name="getLeadingDimensionSize">
        /// Determines the size of the leading dimension.
        /// </param>
        /// <param name="buildStride">Builds a new stride.</param>
        /// <returns>The 3D stride.</returns>
        /// <remarks>
        /// The leading dimension must be less or equal to <see cref="int.MaxValue"/>.
        /// </remarks>
        private static TStride Build3DStride<T, TStride>(
            in LongIndex3D extent,
            Stride3D.GetLeadingDimensionSize getLeadingDimensionSize,
            Stride3D.BuildStride<TStride> buildStride)
            where T : unmanaged
            where TStride : struct, IStride3D
        {
            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0)
                throw new ArgumentOutOfRangeException(nameof(extent));
            if (getLeadingDimensionSize is null)
                throw new ArgumentNullException(nameof(getLeadingDimensionSize));
            if (buildStride is null)
                throw new ArgumentNullException(nameof(buildStride));

            // Get the leading dimension
            long leadingDimSize = getLeadingDimensionSize(extent);
            if (leadingDimSize < 0 || leadingDimSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(extent));

            // Build the stride
            return buildStride(extent, (int)leadingDimSize);
        }

        /// <summary>
        /// Allocates a 3D buffer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The buffer stride type.</typeparam>
        /// <param name="extent">The number of elements to use.</param>
        /// <param name="getLeadingDimensionSize">
        /// Determines the size of the leading dimension.
        /// </param>
        /// <param name="buildStride">Builds a new stride.</param>
        /// <returns>An allocated 3D buffer on the this accelerator.</returns>
        /// <remarks>
        /// The leading dimension must be less or equal to <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer3D<T, TStride> Allocate3D<T, TStride>(
            LongIndex3D extent,
            Stride3D.GetLeadingDimensionSize getLeadingDimensionSize,
            Stride3D.BuildStride<TStride> buildStride)
            where T : unmanaged
            where TStride : struct, IStride3D
        {
            // Build the stride
            var stride = Build3DStride<T, TStride>(
                extent,
                getLeadingDimensionSize,
                buildStride);

            // Allocate the raw chunk of memory
            var baseView = AllocateRaw<T, LongIndex3D, Index3D, TStride>(extent, stride);

            // Create the resulting memory buffer wrapper
            return new MemoryBuffer3D<T, TStride>(
                this,
                new ArrayView3D<T, TStride>(
                    baseView,
                    extent,
                    stride));
        }

        /// <summary>
        /// Allocates a 3D buffer with XY being the leading dimensions.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <returns>An allocated 3D buffer on the this accelerator.</returns>
        /// <remarks>
        /// Since XY are the leading dimension, X * Y must be less or equal to
        /// <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer3D<T, Stride3D.DenseXY> Allocate3DDenseXY<T>(
            LongIndex3D extent)
            where T : unmanaged =>
            Allocate3D<T, Stride3D.DenseXY>(
                extent,
                ex => ex.X * ex.Y,
                (ex, leadingDim) =>
                {
                    IndexTypeExtensions.AssertIntIndexRange(ex.X);
                    return new Stride3D.DenseXY((int)ex.X, leadingDim);
                });

        /// <summary>
        /// Allocates a 3D buffer with ZY being the leading dimensions.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <returns>An allocated 3D buffer on the this accelerator.</returns>
        /// <remarks>
        /// Since YZ are the leading dimension, Y * Z must be less or equal to
        /// <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer3D<T, Stride3D.DenseZY> Allocate3DDenseZY<T>(
            LongIndex3D extent)
            where T : unmanaged =>
            Allocate3D<T, Stride3D.DenseZY>(
                extent,
                ex => ex.Y * ex.Z,
                (ex, leadingDim) =>
                {
                    IndexTypeExtensions.AssertIntIndexRange(ex.Z);
                    return new Stride3D.DenseZY(leadingDim, (int)ex.Z);
                });

        /// <summary>
        /// Allocates a pitched 3D buffer.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The buffer stride type.</typeparam>
        /// <param name="extent">The number of elements to use.</param>
        /// <param name="getLeadingDimensionSize">
        /// Determines the size of the leading dimension.
        /// </param>
        /// <param name="buildStride">Builds a new stride.</param>
        /// <param name="alignmentInBytes">
        /// The alignment in bytes of the leading dimension.
        /// </param>
        /// <returns>An allocated pitched 3D buffer on the this accelerator.</returns>
        private MemoryBuffer3D<T, TStride> Allocate3DPitched<T, TStride>(
            LongIndex3D extent,
            Stride3D.GetLeadingDimensionSize getLeadingDimensionSize,
            Stride3D.BuildStride<TStride> buildStride,
            int alignmentInBytes)
            where T : unmanaged
            where TStride : struct, IStride3D
        {
            // Determines a pitched leading dimension.
            long GetPitchedLeadingDimension(LongIndex3D extent)
            {
                long leadingDim = getLeadingDimensionSize(extent);
                return StrideExtensions.GetPitchedLeadingDimension<T>(
                    leadingDim,
                    alignmentInBytes);
            }

            return Allocate3D<T, TStride>(
                extent,
                GetPitchedLeadingDimension,
                buildStride);
        }

        /// <summary>
        /// Allocates a pitched 3D buffer with XY being the leading dimensions.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <param name="xyAlignmentInBytes">
        /// The alignment in bytes of the leading dimension.
        /// </param>
        /// <returns>An allocated 2D buffer on the this accelerator.</returns>
        /// <remarks>
        /// Since XY are the leading dimensions, X * Y must be less or equal to
        /// <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer3D<T, Stride3D.DenseXY> Allocate3DPitchedXY<T>(
            LongIndex3D extent,
            int xyAlignmentInBytes)
            where T : unmanaged =>
            Allocate3DPitched<T, Stride3D.DenseXY>(
                extent,
                ex => ex.X * ex.Y,
                (ex, leadingDim) =>
                {
                    IndexTypeExtensions.AssertIntIndexRange(ex.X);
                    return new Stride3D.DenseXY((int)ex.X, leadingDim);
                },
                xyAlignmentInBytes);

        /// <summary>
        /// Allocates a pitched 3D buffer with ZY being the leading dimensions.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The number of elements to allocate.</param>
        /// <param name="yzAlignmentInBytes">
        /// The alignment in bytes of the leading dimension.
        /// </param>
        /// <returns>An allocated 3D buffer on the this accelerator.</returns>
        /// <remarks>
        /// Since YZ are the leading dimensions, Y * Z must be less or equal to
        /// <see cref="int.MaxValue"/>.
        /// </remarks>
        public MemoryBuffer3D<T, Stride3D.DenseZY> Allocate3DPitchedZY<T>(
            LongIndex3D extent,
            int yzAlignmentInBytes)
            where T : unmanaged =>
            Allocate3DPitched<T, Stride3D.DenseZY>(
                extent,
                ex => ex.Y * ex.Z,
                (ex, leadingDim) =>
                {
                    IndexTypeExtensions.AssertIntIndexRange(ex.Z);
                    return new Stride3D.DenseZY(leadingDim, (int)ex.Z);
                },
                yzAlignmentInBytes);

        #endregion
    }
}
