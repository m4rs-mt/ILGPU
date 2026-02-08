// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for group intrinsic tests.
/// </summary>
static class GroupKernels
{
    /// <summary>
    /// Store Group.Dimension into data[index].
    /// </summary>
    public static void GroupDimensionKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Group.Dimension;
    }

    /// <summary>
    /// Group.Barrier() between write and read operations.
    /// </summary>
    public static void GroupBarrierKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = index.X;
        Group.Barrier();
        data[index] = data[index] + 1;
    }

    /// <summary>
    /// Store Group.Index into data[index].
    /// </summary>
    public static void GroupIdxKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Group.Index;
    }
}
