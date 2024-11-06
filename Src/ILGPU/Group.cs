// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Group.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.Random;
using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Contains general grid functions.
/// </summary>
public static class Group
{
    #region Properties

    /// <summary>
    /// Returns the linear index withing the scheduled thread group.
    /// </summary>
    /// <returns>The linear group index.</returns>
    public static int Index
    {
        [GroupIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns the dimension of the number of threads per group per grid element
    /// in the scheduled thread grid.
    /// </summary>
    /// <returns>The thread dimension for a single group.</returns>
    public static int Dimension
    {
        [GroupIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns true if the current thread is the first in the group.
    /// </summary>
    public static bool IsFirstThread => Index == 0;

    #endregion

    #region Barriers

    /// <summary>
    /// Executes a thread barrier.
    /// </summary>
    [GroupIntrinsic]
    public static void Barrier() => throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a thread barrier and returns the number of threads for which
    /// the predicate evaluated to true.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>
    /// The number of threads for which the predicate evaluated to true.
    /// </returns>
    [GroupIntrinsic]
    public static int BarrierPopCount(bool predicate) =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a thread barrier and returns true if all threads in a block
    /// fulfills the predicate.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>True, if all threads in a block fulfills the predicate.</returns>
    [GroupIntrinsic]
    public static bool BarrierAnd(bool predicate) =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a thread barrier and returns true if any thread in a block
    /// fulfills the predicate.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>True, if any thread in a block fulfills the predicate.</returns>
    [GroupIntrinsic]
    public static bool BarrierOr(bool predicate) =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Broadcast

    /// <summary>
    /// Performs a broadcast operation that broadcasts the given value
    /// from the specified thread to all other threads in the group.
    /// </summary>
    /// <param name="value">The value to broadcast.</param>
    /// <remarks>
    /// Note that the group index must be the same for all threads in the group.
    /// </remarks>
    [GroupIntrinsic]
    public static T Broadcast<T>(FirstThreadValue<T> value) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Reduce

    /// <summary>
    /// Implements a block-wide reduction algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>All lanes in the first warp contain the reduced value.</returns>
    [GroupIntrinsic]
    public static FirstThreadValue<T> Reduce<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Implements a block-wide reduction algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="value">The current value.</param>
    /// <returns>All threads in the whole group contain the reduced value.</returns>
    [GroupIntrinsic]
    public static T AllReduce<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Scan

    /// <summary>
    /// Performs a group-wide exclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [GroupIntrinsic]
    public static T ExclusiveScan<T, TScanOperation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a group-wide inclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [GroupIntrinsic]
    public static T InclusiveScan<T, TScanOperation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Sort

    /// <summary>
    /// Performs a group-wide radix sort pass.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">The radix sort operation.</typeparam>
    /// <param name="value">The original value in the current lane.</param>
    /// <returns>The sorted value in the current lane.</returns>
    [GroupIntrinsic]
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
        private Warp.RandomScope<TRandomProvider> _scope;

        /// <summary>
        /// Constructs a new random scope.
        /// </summary>
        public RandomScope()
        {
            _scope = Warp.GetRandom<TRandomProvider>();
        }

        /// <summary>
        /// Generates a random int in [0..int.MaxValue].
        /// </summary>
        /// <returns>A random int in [0..int.MaxValue].</returns>
        public int Next() => _scope.Next();

        /// <summary>
        /// Generates a random long in [0..long.MaxValue].
        /// </summary>
        /// <returns>A random long in [0..long.MaxValue].</returns>
        public long NextLong() => _scope.NextLong();

        /// <summary>
        /// Generates a random float in [0..1).
        /// </summary>
        /// <returns>A random float in [0..1).</returns>
        public float NextFloat() => _scope.NextFloat();

        /// <summary>
        /// Generates a random double in [0..1).
        /// </summary>
        /// <returns>A random double in [0..1).</returns>
        public double NextDouble() => _scope.NextDouble();

        /// <summary>
        /// Shifts the current period.
        /// </summary>
        /// <param name="shift">The shift amount.</param>
        public void ShiftPeriod(int shift) => _scope.ShiftPeriod(shift);

        /// <summary>
        /// Commits changes and updates to this random provider to ensure new random
        /// values will be generated by future calls to
        /// <see cref="GetRandom{TRandomProvider}"/>.
        /// </summary>
        public readonly void Commit() => _scope.Commit();
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
    /// Permutes the given value within a group.
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
    /// Permutes the given value within a group.
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
        var threadValue = random.Next() & 0x7fffc000 + Index;
        return Permute(value, threadValue);
    }

    /// <summary>
    /// Permutes the given value within a group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value to permute.</param>
    /// <param name="threadValue">The random number for this thread.</param>
    /// <returns>A permuted value from another random group index.</returns>
    [GroupIntrinsic]
    private static T Permute<T>(T value, int threadValue) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion
}
