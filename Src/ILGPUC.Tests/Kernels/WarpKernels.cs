// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for warp intrinsic tests.
/// </summary>
static class WarpKernels
{
    /// <summary>
    /// Store Warp.Dimension (warp size) into data[index].
    /// </summary>
    public static void WarpSizeKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Warp.Dimension;
    }

    /// <summary>
    /// Store Warp.LaneIndex into data[index].
    /// </summary>
    public static void WarpLaneIdxKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Warp.LaneIndex;
    }

    /// <summary>
    /// Warp.Barrier() between operations.
    /// </summary>
    public static void WarpBarrierKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = index.X;
        Warp.Barrier();
        data[index] = data[index] + 1;
    }
}
