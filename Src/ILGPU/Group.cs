// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Group.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using ILGPU.Runtime.CPU;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Contains general grid functions.
    /// </summary>
    public static class Group
    {
        #region Properties

        /// <summary>
        /// Returns the X index withing the scheduled thread group.
        /// </summary>
        /// <returns>The X grid dimension.</returns>
        public static int IndexX
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupIndex, DeviceConstantDimension3D.X)]
            get => CPURuntimeThreadContext.GroupIndex.X;
        }

        /// <summary>
        /// Returns the Y index withing the scheduled thread group.
        /// </summary>
        /// <returns>The Y grid dimension.</returns>
        public static int IndexY
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupIndex, DeviceConstantDimension3D.Y)]
            get => CPURuntimeThreadContext.GroupIndex.Y;
        }

        /// <summary>
        /// Returns the Z index withing the scheduled thread group.
        /// </summary>
        /// <returns>The Z grid dimension.</returns>
        public static int IndexZ
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupIndex, DeviceConstantDimension3D.Z)]
            get => CPURuntimeThreadContext.GroupIndex.Z;
        }

        /// <summary>
        /// Returns the group index within the scheduled thread group.
        /// </summary>
        /// <returns>The grid index.</returns>
        public static Index3 Index => new Index3(IndexX, IndexY, IndexZ);

        /// <summary>
        /// Returns X the dimension of the number of threads per group per grid element
        /// in the scheduled thread grid.
        /// </summary>
        /// <returns>The X thread dimension for a single group.</returns>
        public static int DimensionX
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupDimension, DeviceConstantDimension3D.X)]
            get => CPURuntimeThreadContext.GroupDimension.X;
        }

        /// <summary>
        /// Returns Y the dimension of the number of threads per group per grid element
        /// in the scheduled thread grid.
        /// </summary>
        /// <returns>The Y thread dimension for a single group.</returns>
        public static int DimensionY
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupDimension, DeviceConstantDimension3D.Y)]
            get => CPURuntimeThreadContext.GroupDimension.Y;
        }

        /// <summary>
        /// Returns Z the dimension of the number of threads per group per grid element
        /// in the scheduled thread grid.
        /// </summary>
        /// <returns>The Z thread dimension for a single group.</returns>
        public static int DimensionZ
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupDimension, DeviceConstantDimension3D.Z)]
            get => CPURuntimeThreadContext.GroupDimension.Z;
        }

        /// <summary>
        /// Returns the dimension of the number of threads per group per grid element
        /// in the scheduled thread grid.
        /// </summary>
        /// <returns>The thread dimension for a single group.</returns>
        public static Index3 Dimension => new Index3(DimensionX, DimensionY, DimensionZ);

        /// <summary>
        /// Returns the linear thread index of the current thread within the current thread group.
        /// </summary>
        public static int LinearIndex => Index.ComputeLinearIndex(Dimension);

        #endregion

        #region Barriers

        /// <summary>
        /// Executes a thread barrier.
        /// </summary>
        [GroupIntrinsic(GroupIntrinsicKind.Barrier)]
        public static void Barrier() =>
            CPURuntimeGroupContext.Current.Barrier();

        /// <summary>
        /// Executes a thread barrier and returns the number of threads for which
        /// the predicate evaluated to true.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>The number of threads for which the predicate evaluated to true.</returns>
        [GroupIntrinsic(GroupIntrinsicKind.BarrierPopCount)]
        public static int BarrierPopCount(bool predicate) =>
            CPURuntimeGroupContext.Current.BarrierPopCount(predicate);

        /// <summary>
        /// Executes a thread barrier and returns true iff all threads in a block
        /// fullfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, iff all threads in a block fullfills the predicate.</returns>
        [GroupIntrinsic(GroupIntrinsicKind.BarrierAnd)]
        public static bool BarrierAnd(bool predicate) =>
            CPURuntimeGroupContext.Current.BarrierAnd(predicate);

        /// <summary>
        /// Executes a thread barrier and returns true iff any thread in a block
        /// fullfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, iff any thread in a block fullfills the predicate.</returns>
        [GroupIntrinsic(GroupIntrinsicKind.BarrierOr)]
        public static bool BarrierOr(bool predicate) =>
            CPURuntimeGroupContext.Current.BarrierOr(predicate);

        #endregion

        #region Broadcast

        /// <summary>
        /// Performs a broadcast operation that broadcasts the given value
        /// from the specified thread to all other threads in the group.
        /// </summary>
        /// <param name="value">The value to broadcast.</param>
        /// <param name="groupIndex">The source thread index within the group.</param>
        /// <remarks>
        /// Note that the group index must be the same for all threads in the group.</remarks>
        [GroupIntrinsic(GroupIntrinsicKind.Broadcast)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Broadcast<T>(T value, int groupIndex)
            where T : struct =>
            CPURuntimeGroupContext.Current.Broadcast(value, groupIndex);

        #endregion
    }
}
