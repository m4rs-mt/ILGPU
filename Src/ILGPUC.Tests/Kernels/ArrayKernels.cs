// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for array access pattern tests.
/// </summary>
static class ArrayKernels
{
    /// <summary>
    /// Basic array-like access: read, compute, write.
    /// </summary>
    public static void SimpleArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        output[index] = value * 2 + 1;
    }

    /// <summary>
    /// Boundary conditions: only write within valid range.
    /// </summary>
    public static void ArrayBoundsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int length)
    {
        if (index.X < length)
            data[index] = index.X;
        else
            data[index] = -1;
    }
}
