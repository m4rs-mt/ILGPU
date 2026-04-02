// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayViewKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for ArrayView operation tests.
/// </summary>
static class ArrayViewKernels
{
    /// <summary>
    /// Store view.Length into result.
    /// </summary>
    public static void LengthKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        result[index] = data.Length;
    }

    /// <summary>
    /// Store 1 if view.IsValid, else 0.
    /// </summary>
    public static void IsValidKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = data.IsValid ? 1 : 0;
    }

    /// <summary>
    /// Read from source, write to target.
    /// </summary>
    public static void LoadStoreKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> source,
        ArrayView1D<int, Stride1D.Dense> target)
    {
        target[index] = source[index];
    }

    /// <summary>
    /// Create SubView and read from it.
    /// </summary>
    public static void SubViewKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result,
        int offset)
    {
        var sub = data.SubView(offset, data.IntLength - offset);
        if (index.X < sub.IntLength)
            result[index] = sub[index];
        else
            result[index] = -1;
    }
}
