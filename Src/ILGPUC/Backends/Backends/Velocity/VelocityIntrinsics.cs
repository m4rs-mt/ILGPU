// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Intrinsics;
using ILGPU.IR.Values;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// Implements and initializes Velocity intrinsics.
    /// </summary>
    static partial class VelocityIntrinsics
    {
        #region Specializers

        /// <summary>
        /// Creates a new Velocity intrinsic.
        /// </summary>
        /// <param name="name">The name of the intrinsic.</param>
        /// <param name="mode">The implementation mode.</param>
        /// <returns>The created intrinsic.</returns>
        private static VelocityIntrinsic CreateIntrinsic(
            string name,
            IntrinsicImplementationMode mode) =>
            new(typeof(VelocityIntrinsics), name, mode);

        /// <summary>
        /// Registers all Velocity intrinsics with the given manager.
        /// </summary>
        /// <param name="manager">The target implementation manager.</param>
        public static void Register(IntrinsicImplementationManager manager)
        {
            RegisterBroadcasts(manager);
            RegisterWarpShuffles(manager);
        }

        #endregion

        #region Broadcasts

        /// <summary>
        /// Registers all broadcast intrinsics with the given manager.
        /// </summary>
        /// <param name="manager">The target implementation manager.</param>
        private static void RegisterBroadcasts(
            IntrinsicImplementationManager manager)
        {
            manager.RegisterBroadcast(
                BroadcastKind.GroupLevel,
                CreateIntrinsic(
                    nameof(GroupAndWarpBroadcast),
                    IntrinsicImplementationMode.Redirect));
            manager.RegisterBroadcast(
                BroadcastKind.WarpLevel,
                CreateIntrinsic(
                    nameof(GroupAndWarpBroadcast),
                    IntrinsicImplementationMode.Redirect));
        }

        /// <summary>
        /// Wraps a single warp and group-broadcast operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T GroupAndWarpBroadcast<T>(T value, int laneIndex)
            where T : unmanaged =>
            Warp.Shuffle(value, laneIndex);

        #endregion
    }
}
