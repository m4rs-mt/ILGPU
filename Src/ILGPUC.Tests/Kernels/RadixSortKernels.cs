// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RadixSortKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.RadixSort;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for warp-level and group-level radix sort operations.
/// </summary>
static class RadixSortKernels
{
    /// <summary>
    /// Warp radix sort of int values in ascending order.
    /// </summary>
    public static void WarpRadixSortAscendingInt32Kernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        int sorted = Warp.RadixSort<int, AscendingInt32>(value);
        data[index] = sorted;
    }

    /// <summary>
    /// Group radix sort of int values in ascending order.
    /// </summary>
    public static void GroupRadixSortAscendingInt32Kernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        int sorted = Group.RadixSort<int, AscendingInt32>(value);
        data[index] = sorted;
    }
}
