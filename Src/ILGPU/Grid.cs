// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Grid.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using ILGPU.Runtime.CPU;
using static ILGPU.IndexTypeExtensions;

namespace ILGPU
{
    /// <summary>
    /// Contains general grid functions.
    /// </summary>
    public static class Grid
    {
        #region Properties

        /// <summary>
        /// Returns the X index withing the scheduled thread grid.
        /// </summary>
        /// <returns>The X grid dimension.</returns>
        public static int IdxX
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridIndex, DeviceConstantDimension3D.X)]
            get => CPURuntimeThreadContext.Current.GridIndex.X;
        }

        /// <summary>
        /// Returns the Y index withing the scheduled thread grid.
        /// </summary>
        /// <returns>The Y grid dimension.</returns>
        public static int IdxY
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridIndex, DeviceConstantDimension3D.Y)]
            get => CPURuntimeThreadContext.Current.GridIndex.Y;
        }

        /// <summary>
        /// Returns the Z index withing the scheduled thread grid.
        /// </summary>
        /// <returns>The Z grid dimension.</returns>
        public static int IdxZ
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridIndex, DeviceConstantDimension3D.Z)]
            get => CPURuntimeThreadContext.Current.GridIndex.Z;
        }

        /// <summary>
        /// Returns the index within the scheduled thread grid.
        /// </summary>
        /// <returns>The grid index.</returns>
        public static Index3D Index => new Index3D(IdxX, IdxY, IdxZ);

        /// <summary>
        /// Returns the X dimension of the scheduled thread grid.
        /// </summary>
        /// <returns>The X grid dimension.</returns>
        public static int DimX
        {
            [GridIntrinsic(
                GridIntrinsicKind.GetGridDimension,
                DeviceConstantDimension3D.X)]
            get => CPURuntimeGroupContext.Current.GridDimension.X;
        }

        /// <summary>
        /// Returns the Y dimension of the scheduled thread grid.
        /// </summary>
        /// <returns>The Y grid dimension.</returns>
        public static int DimY
        {
            [GridIntrinsic(
                GridIntrinsicKind.GetGridDimension,
                DeviceConstantDimension3D.Y)]
            get => CPURuntimeGroupContext.Current.GridDimension.Y;
        }

        /// <summary>
        /// Returns the Z dimension of the scheduled thread grid.
        /// </summary>
        /// <returns>The Z grid dimension.</returns>
        public static int DimZ
        {
            [GridIntrinsic(
                GridIntrinsicKind.GetGridDimension,
                DeviceConstantDimension3D.Z)]
            get => CPURuntimeGroupContext.Current.GridDimension.Z;
        }

        /// <summary>
        /// Returns the dimension of the scheduled thread grid.
        /// </summary>
        /// <returns>The grid dimension.</returns>
        public static Index3D Dimension => new Index3D(DimX, DimY, DimZ);

        /// <summary>
        /// Returns the global index.
        /// </summary>
        public static Index3D GlobalIndex => ComputeGlobalIndex(
            Index,
            Group.Index);

        /// <summary>
        /// Returns the global index using 64-bit integers.
        /// </summary>
        public static LongIndex3D LongGlobalIndex => ComputeLongGlobalIndex(
            Index,
            Group.Index);

        #endregion

        #region Methods

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static Index1D ComputeGlobalIndex(Index1D gridIdx, Index1D groupIdx)
        {
            var groupDim = Group.Dimension;
            AssertIntIndexRange(groupIdx.X + gridIdx.X * (long)groupDim.X);
            return new Index1D(groupIdx + gridIdx * groupDim.X);
        }

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static Index2D ComputeGlobalIndex(Index2D gridIdx, Index2D groupIdx)
        {
            var groupDim = Group.Dimension;
            AssertIntIndexRange(groupIdx.X + gridIdx.X * (long)groupDim.X);
            AssertIntIndexRange(groupIdx.Y + gridIdx.Y * (long)groupDim.Y);
            return new Index2D(
                groupIdx.X + gridIdx.X * groupDim.X,
                groupIdx.Y + gridIdx.Y * groupDim.Y);
        }

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static Index3D ComputeGlobalIndex(Index3D gridIdx, Index3D groupIdx)
        {
            var groupDim = Group.Dimension;
            AssertIntIndexRange(groupIdx.X + gridIdx.X * (long)groupDim.X);
            AssertIntIndexRange(groupIdx.Y + gridIdx.Y * (long)groupDim.Y);
            AssertIntIndexRange(groupIdx.Z + gridIdx.Z * (long)groupDim.Z);
            return new Index3D(
                groupIdx.X + gridIdx.X * groupDim.X,
                groupIdx.Y + gridIdx.Y * groupDim.Y,
                groupIdx.Z + gridIdx.Z * groupDim.Z);
        }

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static LongIndex1D ComputeLongGlobalIndex(
            Index1D gridIdx,
            Index1D groupIdx)
        {
            var groupDim = Group.Dimension;
            return new LongIndex1D(groupIdx + gridIdx * (long)groupDim.X);
        }

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static LongIndex2D ComputeLongGlobalIndex(
            Index2D gridIdx,
            Index2D groupIdx)
        {
            var groupDim = Group.Dimension;
            return new LongIndex2D(
                groupIdx.X + gridIdx.X * (long)groupDim.X,
                groupIdx.Y + gridIdx.Y * (long)groupDim.Y);
        }

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static LongIndex3D ComputeLongGlobalIndex(
            Index3D gridIdx,
            Index3D groupIdx)
        {
            var groupDim = Group.Dimension;
            return new LongIndex3D(
                groupIdx.X + gridIdx.X * (long)groupDim.X,
                groupIdx.Y + gridIdx.Y * (long)groupDim.Y,
                groupIdx.Z + gridIdx.Z * (long)groupDim.Z);
        }

        #endregion
    }
}
