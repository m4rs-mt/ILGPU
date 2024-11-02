// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Warp.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.Random;
using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Contains warp-wide functions.
/// </summary>
public static class Warp
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

    #region Reduce

    /// <summary>
    /// Performs a warp-wide reduction.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>The first lane (lane id = 0) will return reduced result.</returns>
    [WarpIntrinsic]
    public static FirstLaneValue<T> Reduce<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a warp-wide reduction.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>All lanes will return the reduced result.</returns>
    [WarpIntrinsic]
    public static T AllReduce<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Scan

    /// <summary>
    /// Performs a warp-wide exclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [WarpIntrinsic]
    public static T ExclusiveScan<T, TScan>(T value)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a warp-wide inclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [WarpIntrinsic]
    public static T InclusiveScan<T, TScan>(T value)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Sort

    /// <summary>
    /// Performs a warp-wide radix sort operation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The radix sort operation type.
    /// </typeparam>
    /// <param name="value">The original value in the current lane.</param>
    /// <returns>The sorted value for the current lane.</returns>
    [WarpIntrinsic]
    public static T RadixSort<T, TRadixSortOperation>(T value)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T> =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Random

    /// <summary>
    /// A wrapped random provider valid in the scope of the current warp.
    /// </summary>
    /// <typeparam name="TRandomProvider"></typeparam>
    public struct RandomScope<TRandomProvider> : IRandomProvider
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        private readonly FirstLaneRandomProviderView<TRandomProvider> _view;
        private TRandomProvider _randomProvider;

        /// <summary>
        /// Constructs a new random scope.
        /// </summary>
        public RandomScope()
        {
            _view = IntrinsicProvider.Provide<
                FirstLaneValue<TRandomProvider>,
                FirstLaneRandomProviderView<TRandomProvider>,
                FirstLaneRandomProvider<TRandomProvider>>();

            var firstLaneValue = _view[GlobalIndex];
            var value = Broadcast(firstLaneValue);
            value.ShiftPeriod(LaneIndex);

            _randomProvider = value;
        }

        /// <summary>
        /// Generates a random int in [0..int.MaxValue].
        /// </summary>
        /// <returns>A random int in [0..int.MaxValue].</returns>
        public int Next() => _randomProvider.Next();

        /// <summary>
        /// Generates a random long in [0..long.MaxValue].
        /// </summary>
        /// <returns>A random long in [0..long.MaxValue].</returns>
        public long NextLong() => _randomProvider.NextLong();

        /// <summary>
        /// Generates a random float in [0..1).
        /// </summary>
        /// <returns>A random float in [0..1).</returns>
        public float NextFloat() => _randomProvider.NextFloat();

        /// <summary>
        /// Generates a random double in [0..1).
        /// </summary>
        /// <returns>A random double in [0..1).</returns>
        public double NextDouble() => _randomProvider.NextDouble();

        /// <summary>
        /// Shifts the current period.
        /// </summary>
        /// <param name="shift">The shift amount.</param>
        public void ShiftPeriod(int shift) => _randomProvider.ShiftPeriod(shift);

        /// <summary>
        /// Commits changes and updates to this random provider to ensure new random
        /// values will be generated by future calls to
        /// <see cref="GetRandom{TRandomProvider}"/>.
        /// </summary>
        public readonly void Commit()
        {
            if (IsFirstLane)
                _view[GlobalIndex] = new(_randomProvider);
        }
    }

    /// <summary>
    /// Creates a new random number of the current warp.
    /// </summary>
    /// <typeparam name="TRandomProvider">
    /// The random number provider type.
    /// </typeparam>
    /// <returns>A permuted value from another random warp lane.</returns>
    public static RandomScope<TRandomProvider> GetRandom<TRandomProvider>()
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
        new();

    #endregion

    #region Permute

    /// <summary>
    /// Permutes the given value within a warp.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">
    /// The random number provider type.
    /// </typeparam>
    /// <param name="value">The value to permute.</param>
    /// <returns>A permuted value from another random warp lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Permute<T, TRandomProvider>(T value)
        where T : unmanaged
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        var randomScope = GetRandom<TRandomProvider>();
        var result = Permute(value, ref randomScope);
        randomScope.Commit();
        return result;
    }

    /// <summary>
    /// Permutes the given value within a warp.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">
    /// The random number provider type.
    /// </typeparam>
    /// <param name="value">The value to permute.</param>
    /// <param name="random">The random number generator.</param>
    /// <returns>A permuted value from another random warp lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Permute<T, TRandomProvider>(
        T value,
        ref TRandomProvider random)
        where T : unmanaged
        where TRandomProvider : struct, IRandomProvider
    {
        var laneValue = random.Next() & 0x7fffc000 + LaneIndex;
        return Permute(value, laneValue);
    }

    /// <summary>
    /// Permutes the given value within a warp.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value to permute.</param>
    /// <param name="laneValue">The random number for this lane.</param>
    /// <returns>A permuted value from another random warp lane.</returns>
    [WarpIntrinsic]
    private static T Permute<T>(T value, int laneValue) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion
}
