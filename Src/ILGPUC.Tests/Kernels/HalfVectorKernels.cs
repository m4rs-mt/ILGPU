// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfVectorKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Vectors;
using System;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Float16 vector types loaded from and stored to Half views.
/// </summary>
static class HalfVectorKernels
{
    public static void Float16x4ArithmeticKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> a,
        ArrayView1D<Half, Stride1D.Dense> b,
        ArrayView1D<Half, Stride1D.Dense> result)
    {
        var x = Float16x4.FromElementView(a, index * 4);
        var y = Float16x4.FromElementView(b, index * 4);
        var r = x * y + Float16x4.Max(x, y) - Float16x4.Abs(x) / y;
        r.ToElementView(result, index * 4);
    }

    public static void Float16x2MagnitudeKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> flags)
    {
        var x = Float16x2.FromElementView(data, index * 2);
        var r = Float16x2.MaxMagnitude(x, Float16x2.One) + Float16x2.Clamp(
            x, Float16x2.Zero, Float16x2.One);
        r.ToElementView(data, index * 2);
        flags[index] = Float16x2.IsNaN(r) ? 1 : Float16x2.IsNegative(r) ? 2 : 0;
    }

    /// <summary>
    /// Copies Float16x2 values, which loads and stores the structure without
    /// a value of the half type itself.
    /// </summary>
    public static void CopyFloat16x2Kernel(
        Index1D index,
        ArrayView1D<Float16x2, Stride1D.Dense> source,
        ArrayView1D<Float16x2, Stride1D.Dense> target)
    {
        target[index] = source[index];
    }
}
