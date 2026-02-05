// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompareIntKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Comparison operations on integer types.
/// </summary>
static class CompareIntKernels
{
    public static void LessThanKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged, IComparisonOperators<T, T, bool>
    {
        result[index] = (a[index] < b[index]) ? 1 : 0;
    }

    public static void LessEqualKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged, IComparisonOperators<T, T, bool>
    {
        result[index] = (a[index] <= b[index]) ? 1 : 0;
    }

    public static void GreaterThanKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged, IComparisonOperators<T, T, bool>
    {
        result[index] = (a[index] > b[index]) ? 1 : 0;
    }

    public static void GreaterEqualKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged, IComparisonOperators<T, T, bool>
    {
        result[index] = (a[index] >= b[index]) ? 1 : 0;
    }

    public static void EqualKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged, IEqualityOperators<T, T, bool>
    {
        result[index] = (a[index] == b[index]) ? 1 : 0;
    }

    public static void NotEqualKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
        where T : unmanaged, IEqualityOperators<T, T, bool>
    {
        result[index] = (a[index] != b[index]) ? 1 : 0;
    }
}
