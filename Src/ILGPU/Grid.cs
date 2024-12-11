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
using System;
using System.Runtime.CompilerServices;

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
    public static long GlobalThreadIndex => Index * Group.Dimension + Group.Index;

    /// <summary>
    /// Returns the loop stride for a grid-stride loop.
    /// </summary>
    public static long GridStrideLoopStride => Dimension * Group.Dimension;

    /// <summary>
    /// Returns true if this is the first group in the thread grid.
    /// </summary>
    public static bool IsFirstGroup => Index == 0;

    #endregion

    #region Methods

    /// <summary>
    /// Performs a grid-stride loop.
    /// </summary>
    /// <param name="numIterationsPerGroup">Number of iterations per group.</param>
    /// <param name="loopBody">The loop body.</param>
    public static void GridStrideLoop(int numIterationsPerGroup, Action<long> loopBody)
    {
        for (int i = 0; i < numIterationsPerGroup; ++i)
        {
            long index = GlobalThreadIndex + i * GridStrideLoopStride;
            loopBody(index);
        }
    }

    /// <summary>
    /// Returns a kernel extent (a grouped index) with the maximum number of groups
    /// using the maximum number of threads per group to launch common grid-stride
    /// loop kernels.
    /// </summary>
    /// <param name="kernelSize">The maximum kernel dimension.</param>
    /// <param name="numDataElements">
    /// The number of parallel data elements to process.
    /// </param>
    /// <param name="numIterationsPerGroup">
    /// The number of loop iterations per group.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static KernelConfig ComputeGridStrideKernelConfig(
        this KernelSize kernelSize,
        long numDataElements,
        out int numIterationsPerGroup)
    {
        if (numDataElements < 1)
            throw new ArgumentOutOfRangeException(nameof(numDataElements));

        var numParallelGroups = XMath.DivRoundUp(
            numDataElements,
            kernelSize.GroupSize);
        long dimension = XMath.Min(kernelSize.GridSize, numParallelGroups);

        numIterationsPerGroup = (int)XMath.DivRoundUp(
            numDataElements,
            dimension * kernelSize.GroupSize);

        return (dimension, kernelSize.GroupSize);
    }

    #endregion

    #region Memory Fence

    /// <summary>
    /// A memory fence at the device level.
    /// </summary>
    [GridIntrinsic]
    public static void MemoryFence() => throw new InvalidKernelOperationException();

    /// <summary>
    /// A memory fence at the system level.
    /// </summary>
    [GridIntrinsic]
    public static void SystemMemoryFence() => throw new InvalidKernelOperationException();

    #endregion
}
