// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelEntryPointKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for index type entry point tests.
/// </summary>
static class KernelEntryPointKernels
{
    /// <summary>
    /// Uses Index1D entry point.
    /// </summary>
    public static void Index1DKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = index.X;
    }

    /// <summary>
    /// Uses Index2D entry point. Stores X + Y * 1000 into a flat array at X.
    /// </summary>
    public static void Index2DKernel(
        Index2D index, ArrayView1D<int, Stride1D.Dense> data, int width)
    {
        int linear = index.X + index.Y * width;
        data[linear] = index.X + index.Y * 1000;
    }

    /// <summary>
    /// Uses Index3D entry point. Stores X + Y * 100 + Z * 10000.
    /// </summary>
    public static void Index3DKernel(
        Index3D index, ArrayView1D<int, Stride1D.Dense> data,
        int width, int height)
    {
        int linear = index.X + index.Y * width + index.Z * width * height;
        data[linear] = index.X + index.Y * 100 + index.Z * 10000;
    }
}
