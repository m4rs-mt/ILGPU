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

namespace ILGPU;

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

    /// <summary>
    /// Returns true if the current thread is the last in the group.
    /// </summary>
    public static bool IsLastThread => Index == Dimension - 1;

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
    /// <param name="groupIndex">The source thread index within the group.</param>
    /// <remarks>
    /// Note that the group index must be the same for all threads in the group.
    /// </remarks>
    [GroupIntrinsic]
    public static T Broadcast<T>(T value, int groupIndex) where T : unmanaged =>
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
    public static T Reduce<T, TReduction>(T value)
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

    /// <summary>
    /// Performs a group-wide exclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <param name="boundaries">The scan boundaries.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [GroupIntrinsic]
    public static T ExclusiveScanWithBoundaries<T, TScanOperation>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a group-wide inclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <param name="boundaries">The scan boundaries.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [GroupIntrinsic]
    public static T InclusiveScanWithBoundaries<T, TScanOperation>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Prepares for the next iteration of a group-wide exclusive scan within the
    /// same kernel.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
    /// <param name="leftBoundary">The left boundary value.</param>
    /// <param name="rightBoundary">The right boundary value.</param>
    /// <param name="currentValue">The current value.</param>
    /// <returns>The starting value for the next iteration.</returns>
    [GroupIntrinsic]
    public static T ExclusiveScanNextIteration<T, TScanOperation>(
        T leftBoundary,
        T rightBoundary,
        T currentValue)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Prepares for the next iteration of a group-wide inclusive scan within the
    /// same kernel.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
    /// <param name="leftBoundary">The left boundary value.</param>
    /// <param name="rightBoundary">The right boundary value.</param>
    /// <param name="currentValue">The current value.</param>
    /// <returns>The starting value for the next iteration.</returns>
    [GroupIntrinsic]
    public static T InclusiveScanNextIteration<T, TScanOperation>(
        T leftBoundary,
        T rightBoundary,
        T currentValue)
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

    #region Permute

    /// <summary>
    /// Permutes the given value within a warp.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">
    /// The random number provider type.
    /// </typeparam>
    /// <param name="value">The value to permute.</param>
    /// <param name="random">The random number generator.</param>
    /// <returns>A permuted value from another random group index.</returns>
    [GroupIntrinsic]
    public static T Permute<T, TRandomProvider>(T value, ref TRandomProvider random)
        where T : unmanaged
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
        throw new InvalidKernelOperationException();

    #endregion
}
