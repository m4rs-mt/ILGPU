// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ScanReduceExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.ScanReduce;

namespace ILGPU;

partial class Group
{
    #region Scan

    /// <summary>
    /// Performs a group-wide exclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [GroupIntrinsic]
    public static T ExclusiveScan<T, TScan>(T value)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a group-wide exclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <param name="boundaries">The scan element boundaries.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [GroupIntrinsic]
    public static T ExclusiveScan<T, TScan>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a group-wide inclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [GroupIntrinsic]
    public static T InclusiveScan<T, TScan>(T value)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a group-wide inclusive scan.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TScan">The type of the warp scan logic.</typeparam>
    /// <param name="value">The value to scan.</param>
    /// <param name="boundaries">The scan element boundaries.</param>
    /// <returns>The resulting value for the current lane.</returns>
    [GroupIntrinsic]
    public static T InclusiveScan<T, TScan>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScan : struct, IScanReduceOperation<T> =>
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
}

partial class Warp
{
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
}
