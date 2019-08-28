// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Grid.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using ILGPU.Runtime.CPU;

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
        public static int IndexX
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridIndex, DeviceConstantDimension3D.X)]
            get => CPURuntimeThreadContext.GridIndex.X;
        }

        /// <summary>
        /// Returns the Y index withing the scheduled thread grid.
        /// </summary>
        /// <returns>The Y grid dimension.</returns>
        public static int IndexY
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridIndex, DeviceConstantDimension3D.Y)]
            get => CPURuntimeThreadContext.GridIndex.Y;
        }

        /// <summary>
        /// Returns the Z index withing the scheduled thread grid.
        /// </summary>
        /// <returns>The Z grid dimension.</returns>
        public static int IndexZ
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridIndex, DeviceConstantDimension3D.Z)]
            get => CPURuntimeThreadContext.GridIndex.Z;
        }

        /// <summary>
        /// Returns the index within the scheduled thread grid.
        /// </summary>
        /// <returns>The grid index.</returns>
        public static Index3 Index => new Index3(IndexX, IndexY, IndexZ);

        /// <summary>
        /// Returns the X dimension of the scheduled thread grid.
        /// </summary>
        /// <returns>The X grid dimension.</returns>
        public static int DimensionX
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridDimension, DeviceConstantDimension3D.X)]
            get => CPURuntimeThreadContext.GridDimension.X;
        }

        /// <summary>
        /// Returns the Y dimension of the scheduled thread grid.
        /// </summary>
        /// <returns>The Y grid dimension.</returns>
        public static int DimensionY
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridDimension, DeviceConstantDimension3D.Y)]
            get => CPURuntimeThreadContext.GridDimension.Y;
        }

        /// <summary>
        /// Returns the Z dimension of the scheduled thread grid.
        /// </summary>
        /// <returns>The Z grid dimension.</returns>
        public static int DimensionZ
        {
            [GridIntrinsic(GridIntrinsicKind.GetGridDimension, DeviceConstantDimension3D.Z)]
            get => CPURuntimeThreadContext.GridDimension.Z;
        }

        /// <summary>
        /// Returns the dimension of the scheduled thread grid.
        /// </summary>
        /// <returns>The grid dimension.</returns>
        public static Index3 Dimension => new Index3(DimensionX, DimensionY, DimensionZ);

        #endregion

        #region Methods

        /// <summary>
        /// Computes the global index of a grouped index (gridIdx, groupIdx).
        /// </summary>
        /// <param name="index">The grouped index.</param>
        /// <returns>The computes global index.</returns>
        public static Index ComputeGlobalIndex(GroupedIndex index) =>
            ComputeGlobalIndex(index.GridIdx, index.GroupIdx);

        /// <summary>
        /// Computes the global index of a grouped index (gridIdx, groupIdx).
        /// </summary>
        /// <param name="index">The grouped index.</param>
        /// <returns>The computes global index.</returns>
        public static Index2 ComputeGlobalIndex(GroupedIndex2 index) =>
            ComputeGlobalIndex(index.GridIdx, index.GroupIdx);

        /// <summary>
        /// Computes the global index of a grouped index (gridIdx, groupIdx).
        /// </summary>
        /// <param name="index">The grouped index.</param>
        /// <returns>The computes global index.</returns>
        public static Index3 ComputeGlobalIndex(GroupedIndex3 index) =>
            ComputeGlobalIndex(index.GridIdx, index.GroupIdx);

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static Index ComputeGlobalIndex(Index gridIdx, Index groupIdx) =>
            groupIdx + gridIdx * Group.Dimension.X;

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static Index2 ComputeGlobalIndex(Index2 gridIdx, Index2 groupIdx)
        {
            var groupDim = Group.Dimension;
            return new Index2(
                groupIdx.X + gridIdx.X * groupDim.X,
                groupIdx.Y + gridIdx.Y * groupDim.Y);
        }

        /// <summary>
        /// Computes the global index of a given gridIdx and a groupIdx.
        /// </summary>
        /// <param name="gridIdx">The grid index.</param>
        /// <param name="groupIdx">The group index.</param>
        /// <returns>The computes global index.</returns>
        public static Index3 ComputeGlobalIndex(Index3 gridIdx, Index3 groupIdx)
        {
            var groupDim = Group.Dimension;
            return new Index3(
                groupIdx.X + gridIdx.X * groupDim.X,
                groupIdx.Y + gridIdx.Y * groupDim.Y,
                groupIdx.Z + gridIdx.Z * groupDim.Z);
        }

        #endregion
    }
}
