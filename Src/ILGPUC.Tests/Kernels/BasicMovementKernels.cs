// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicMovementKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for data movement and barrier tests.
/// </summary>
static class BasicMovementKernels
{
    /// <summary>
    /// Barrier ordering: write, barrier, then read.
    /// Uses Group.Barrier() to synchronize threads.
    /// </summary>
    public static void BarrierOrderingKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        data[index] = index.X * 2;
        Group.Barrier();
        result[index] = data[index] + 1;
    }

    /// <summary>
    /// Copy: read from one view, write to another.
    /// </summary>
    public static void CopyKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> source,
        ArrayView1D<int, Stride1D.Dense> target)
    {
        target[index] = source[index];
    }
}
