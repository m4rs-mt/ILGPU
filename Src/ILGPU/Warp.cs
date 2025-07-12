// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Warp.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Contains warp-wide functions.
/// </summary>
public static partial class Warp
{
    #region General properties

    /// <summary>
    /// Returns the warp size.
    /// </summary>
    public static int Dimension
    {
        [WarpIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns the current lane index [0, WarpSize - 1].
    /// </summary>
    public static int LaneIndex
    {
        [WarpIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns true if the current lane is the first lane.
    /// </summary>
    public static bool IsFirstLane => LaneIndex == 0;

    /// <summary>
    /// Returns true if the current lane is the last lane.
    /// </summary>
    public static bool IsLastLane => LaneIndex == Dimension - 1;

    /// <summary>
    /// Returns the current warp index in the range [0, NumUsedWarps - 1] in the current
    /// group.
    /// </summary>
    /// <returns>The current warp index in the range [0, NumUsedWarps - 1].</returns>
    public static int Index
    {
        [WarpIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns true if the current warp is the first warp in the group.
    /// </summary>
    public static bool IsFirstWarp => Index == 0;

    /// <summary>
    /// Returns a lane bit mask including all lanes in a single warp.
    /// </summary>
    public static int Mask => Dimension - 1;

    /// <summary>
    /// Returns the current warp index in the range [0, NumUsedWarps - 1] in the current
    /// grid.
    /// </summary>
    /// <returns>The current warp index in the range [0, NumUsedWarps - 1].</returns>
    public static long GlobalIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int groupStride = XMath.DivRoundUp(Group.Dimension, Dimension);
            return Grid.Index * groupStride + Index;
        }
    }

    #endregion

    #region Barriers

    /// <summary>
    /// Executes a thread barrier in the scope of a warp.
    /// </summary>
    [WarpIntrinsic]
    public static void Barrier() => throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a thread barrier and returns the number of lanes for which
    /// the predicate evaluated to true.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>
    /// The number of lanes for which the predicate evaluated to true.
    /// </returns>
    [WarpIntrinsic]
    public static int BarrierPopCount(bool predicate) =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a thread barrier and returns true if all lanes in a block
    /// fulfills the predicate.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>True, if all lanes in a block fulfills the predicate.</returns>
    [WarpIntrinsic]
    public static bool BarrierAnd(bool predicate) =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a lanes barrier and returns true if any lanes in a block
    /// fulfills the predicate.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>True, if any lanes in a block fulfills the predicate.</returns>
    [WarpIntrinsic]
    public static bool BarrierOr(bool predicate) =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Shuffle

    /// <summary>
    /// Performs a shuffle operation. It returns the value of the variable
    /// in the context of the specified source lane.
    /// The width of the shuffle operation is the warp size.
    /// </summary>
    /// <typeparam name="T">The value type to shuffle.</typeparam>
    /// <param name="variable">The source variable to shuffle.</param>
    /// <param name="sourceLane">The source lane.</param>
    /// <returns>
    /// The value of the variable in the scope of the desired lane.
    /// </returns>
    /// <remarks>
    /// Note that all threads in a warp should participate in the shuffle operation.
    /// </remarks>
    [WarpIntrinsic]
    public static T Shuffle<T>(T variable, int sourceLane)
        where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Shuffle Down

    /// <summary>
    /// Performs a shuffle operation. It returns the value of the variable
    /// in the context of the lane with the id current lane + delta.
    /// The width of the shuffle operation is the warp size.
    /// </summary>
    /// <typeparam name="T">The value type to shuffle.</typeparam>
    /// <param name="variable">The source variable to shuffle.</param>
    /// <param name="delta">The delta to add to the current lane.</param>
    /// <returns>
    /// The value of the variable in the scope of the desired lane.
    /// </returns>
    /// <remarks>
    /// Note that all threads in a warp should participate in the shuffle operation.
    /// </remarks>
    [WarpIntrinsic]
    public static T ShuffleDown<T>(T variable, int delta) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Shuffle Up

    /// <summary>
    /// Performs a shuffle operation. It returns the value of the variable
    /// in the context of the lane with the id current lane - delta.
    /// The width of the shuffle operation is the warp size.
    /// </summary>
    /// <typeparam name="T">The value type to shuffle.</typeparam>
    /// <param name="variable">The source variable to shuffle.</param>
    /// <param name="delta">The delta to subtract to the current lane.</param>
    /// <returns>
    /// The value of the variable in the scope of the desired lane.
    /// </returns>
    /// <remarks>
    /// Note that all threads in a warp should participate in the shuffle operation.
    /// </remarks>
    [WarpIntrinsic]
    public static T ShuffleUp<T>(T variable, int delta) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Shuffle Xor

    /// <summary>
    /// Performs a shuffle operation. It returns the value of the variable
    /// in the context of the lane with the id current lane xor mask.
    /// The width of the shuffle operation is the warp size.
    /// </summary>
    /// <typeparam name="T">The type to shuffle.</typeparam>
    /// <param name="variable">The source variable to shuffle.</param>
    /// <param name="mask">The mask to xor to the current lane.</param>
    /// <returns>
    /// The value of the variable in the scope of the desired lane.
    /// </returns>
    /// <remarks>
    /// Note that all threads in a warp should participate in the shuffle operation.
    /// </remarks>
    [WarpIntrinsic]
    public static T ShuffleXor<T>(T variable, int mask) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Broadcast

    /// <summary>
    /// Performs a broadcast operation that broadcasts the given value
    /// from the specified thread to all other threads in the warp.
    /// </summary>
    /// <typeparam name="T">The type to broadcast.</typeparam>
    /// <param name="value">The value to broadcast.</param>
    /// <remarks>
    /// Note that the source lane must be the same for all threads in the warp.
    /// </remarks>
    [WarpIntrinsic]
    public static T Broadcast<T>(FirstLaneValue<T> value) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion
}
