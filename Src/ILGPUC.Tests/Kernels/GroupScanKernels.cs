// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupScanKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for group-level scan (prefix sum) operations
/// using the lambda-based API.
/// </summary>
static class GroupScanKernels
{
    /// <summary>
    /// Group inclusive scan with addition.
    /// </summary>
    public static void GroupInclusiveScanAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 1;
        int result = Group.InclusiveScan(value, 0, (a, b) => a + b);
        data[index] = result;
    }

    /// <summary>
    /// Group exclusive scan with addition.
    /// </summary>
    public static void GroupExclusiveScanAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 1;
        int result = Group.ExclusiveScan(value, 0, (a, b) => a + b);
        data[index] = result;
    }
}
