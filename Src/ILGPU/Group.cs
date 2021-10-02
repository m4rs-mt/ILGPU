// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Group.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
        public static int IdxX
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupIndex, DeviceConstantDimension3D.X)]
            get => CPURuntimeThreadContext.Current.GroupIndex.X;
        }

        /// <summary>
        /// Returns the Y index withing the scheduled thread group.
        /// </summary>
        /// <returns>The Y grid dimension.</returns>
        public static int IdxY
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupIndex, DeviceConstantDimension3D.Y)]
            get => CPURuntimeThreadContext.Current.GroupIndex.Y;
        }

        /// <summary>
        /// Returns the Z index withing the scheduled thread group.
        /// </summary>
        /// <returns>The Z grid dimension.</returns>
        public static int IdxZ
        {
            [GridIntrinsic(GridIntrinsicKind.GetGroupIndex, DeviceConstantDimension3D.Z)]
            get => CPURuntimeThreadContext.Current.GroupIndex.Z;
        }

        /// <summary>
        /// Returns the group index within the scheduled thread group.
        /// </summary>
        /// <returns>The grid index.</returns>
        public static Index3D Index => new Index3D(IdxX, IdxY, IdxZ);

        /// <summary>
        /// Returns X the dimension of the number of threads per group per grid element
        /// in the scheduled thread grid.
        /// </summary>
        /// <returns>The X thread dimension for a single group.</returns>
        public static int DimX
        {
            [GridIntrinsic(
                GridIntrinsicKind.GetGroupDimension,
                DeviceConstantDimension3D.X)]
            get => CPURuntimeGroupContext.Current.GroupDimension.X;
        }

        /// <summary>
        /// Returns Y the dimension of the number of threads per group per grid element
        /// in the scheduled thread grid.
        /// </summary>
        /// <returns>The Y thread dimension for a single group.</returns>
        public static int DimY
        {
            [GridIntrinsic(
                GridIntrinsicKind.GetGroupDimension,
                DeviceConstantDimension3D.Y)]
            get => CPURuntimeGroupContext.Current.GroupDimension.Y;
        }

        /// <summary>
        /// Returns Z the dimension of the number of threads per group per grid element
        /// in the scheduled thread grid.
        /// </summary>
        /// <returns>The Z thread dimension for a single group.</returns>
        public static int DimZ
        {
            [GridIntrinsic(
                GridIntrinsicKind.GetGroupDimension,
                DeviceConstantDimension3D.Z)]
            get => CPURuntimeGroupContext.Current.GroupDimension.Z;
        }

        /// <summary>
        /// Returns the dimension of the number of threads per group per grid element
        /// in the scheduled thread grid.
        /// </summary>
        /// <returns>The thread dimension for a single group.</returns>
        public static Index3D Dimension => new Index3D(DimX, DimY, DimZ);

        /// <summary>
        /// Returns the linear thread index of the current thread within the current
        /// thread group.
        /// </summary>
        public static int LinearIndex =>
            Stride3D.DenseXY.ComputeElementIndex(Index, Dimension);

        /// <summary>
        /// Returns true if the current thread is the first in the group.
        /// </summary>
        public static bool IsFirstThread =>
            IdxX == 0 & IdxY == 0 & IdxZ == 0;

        /// <summary>
        /// Returns true if the current thread is the last in the group.
        /// </summary>
        public static bool IsLastThread =>
           (IdxX == DimX - 1) &
           (IdxY == DimY - 1) &
           (IdxZ == DimZ - 1);

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
        /// <returns>
        /// The number of threads for which the predicate evaluated to true.
        /// </returns>
        [GroupIntrinsic(GroupIntrinsicKind.BarrierPopCount)]
        public static int BarrierPopCount(bool predicate) =>
            CPURuntimeGroupContext.Current.BarrierPopCount(predicate);

        /// <summary>
        /// Executes a thread barrier and returns true if all threads in a block
        /// fulfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, if all threads in a block fulfills the predicate.</returns>
        [GroupIntrinsic(GroupIntrinsicKind.BarrierAnd)]
        public static bool BarrierAnd(bool predicate) =>
            CPURuntimeGroupContext.Current.BarrierAnd(predicate);

        /// <summary>
        /// Executes a thread barrier and returns true if any thread in a block
        /// fulfills the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to check.</param>
        /// <returns>True, if any thread in a block fulfills the predicate.</returns>
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
        /// Note that the group index must be the same for all threads in the group.
        /// </remarks>
        [GroupIntrinsic(GroupIntrinsicKind.Broadcast)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Broadcast<T>(T value, int groupIndex)
            where T : unmanaged =>
            CPURuntimeGroupContext.Current.Broadcast(value, groupIndex);

        #endregion
    }
}
