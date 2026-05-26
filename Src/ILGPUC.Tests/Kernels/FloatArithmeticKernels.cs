// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloatArithmeticKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Floating-point arithmetic without a direct C operator.
/// </summary>
static class FloatArithmeticKernels
{
    public static void FloatRemKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> a,
        ArrayView1D<float, Stride1D.Dense> b,
        ArrayView1D<float, Stride1D.Dense> result)
    {
        result[index] = a[index] % b[index];
    }

    public static void DoubleRemKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> a,
        ArrayView1D<double, Stride1D.Dense> b,
        ArrayView1D<double, Stride1D.Dense> result)
    {
        result[index] = a[index] % b[index];
    }

    public static void FloatTruncateRoundKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data)
    {
        float x = data[index];
        data[index] = System.MathF.Truncate(x) + System.MathF.Round(x) +
            System.MathF.Round(x, System.MidpointRounding.AwayFromZero) +
            System.MathF.Round(x, 2);
    }

    public static void DoubleTruncateRoundKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> data)
    {
        double x = data[index];
        data[index] = System.Math.Truncate(x) + System.Math.Round(x) +
            System.Math.Round(x, System.MidpointRounding.AwayFromZero) +
            System.Math.Round(x, 2);
    }
}
