// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupReduceKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for group-level reduce operations
/// using the lambda-based API.
/// </summary>
static class GroupReduceKernels
{
    /// <summary>
    /// Group AllReduce with addition: all threads get the sum.
    /// </summary>
    public static void GroupAllReduceAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 1;
        int result = Group.AllReduce(value, 0, (a, b) => a + b);
        data[index] = result;
    }

    /// <summary>
    /// Group Reduce with addition: only first thread gets the result.
    /// </summary>
    public static void GroupReduceAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 1;
        int result = Group.Reduce(value, 0, (a, b) => a + b);
        data[index] = result;
    }
}
