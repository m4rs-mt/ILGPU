// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GridKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for grid intrinsic tests.
/// </summary>
static class GridKernels
{
    /// <summary>
    /// Store Grid.Dimension into data[index].
    /// </summary>
    public static void GridDimensionKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> data)
    {
        data[index] = Grid.Dimension;
    }

    /// <summary>
    /// Store Grid.Index into data[index].
    /// </summary>
    public static void GridIndexKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> data)
    {
        data[index] = Grid.Index;
    }
}
