// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Grid.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.Runtime;
using System;

namespace ILGPU;

/// <summary>
/// Contains general grid functions.
/// </summary>
public static partial class Grid
{
    #region Properties

    /// <summary>
    /// Returns the linear index withing the scheduled thread grid.
    /// </summary>
    /// <returns>The linear grid dimension.</returns>
    public static long Index
    {
        [GridIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns the dimension of the scheduled thread grid.
    /// </summary>
    /// <returns>The grid dimension.</returns>
    public static long Dimension
    {
        [GridIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns the linear thread index of the current thread within the current
    /// thread grid.
    /// </summary>
    public static long GlobalIndex => Index * Group.Dimension + Group.Index;

    /// <summary>
    /// Returns the loop stride for a grid-stride loop.
    /// </summary>
    public static long GridStrideLoopStride => Dimension * Group.Dimension;

    #endregion

    #region Methods

    /// <summary>
    /// Performs a grid-stride loop.
    /// </summary>
    /// <param name="length">The global length.</param>
    /// <param name="loopBody">The loop body.</param>
    public static void GridStrideLoop(long length, Action<long> loopBody)
    {
        long stride = GridStrideLoopStride;
        for (long idx = GlobalIndex; idx < length; idx += stride)
            loopBody(idx);
    }

    /// <summary>
    /// Returns a kernel extent (a grouped index) with the maximum number of groups
    /// using the maximum number of threads per group to launch common grid-stride
    /// loop kernels.
    /// </summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="numDataElements">
    /// The number of parallel data elements to process.
    /// </param>
    /// <param name="numIterationsPerGroup">
    /// The number of loop iterations per group.
    /// </param>
    [NotInsideKernel]
    public static (Index1D, Index1D) ComputeGridStrideLoopExtent(
        this Accelerator accelerator,
        Index1D numDataElements,
        out int numIterationsPerGroup)
    {
        var (gridDim, groupDim) = accelerator.MaxNumGroupsExtent;

        var numParallelGroups = XMath.DivRoundUp(numDataElements, groupDim);
        var dimension = XMath.Min(gridDim, numParallelGroups);

        numIterationsPerGroup =
            XMath.DivRoundUp(numDataElements, dimension * groupDim);

        return (dimension, groupDim);
    }

    #endregion
}
