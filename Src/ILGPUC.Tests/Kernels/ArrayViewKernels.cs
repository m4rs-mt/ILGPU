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

    /// <summary>
    /// Stores the length of the empty view, whose single use is emitted
    /// inline.
    /// </summary>
    public static void EmptyViewIntLengthKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = ArrayView<int>.Empty.IntLength;
    }

    /// <summary>
    /// Stores the members of the empty view, a constant view without
    /// elements.
    /// </summary>
    public static void EmptyViewMembersKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        var view = ArrayView<int>.Empty;
        result[index * 7 + 0] = view.IntLength;
        result[index * 7 + 1] = view.Length;
        result[index * 7 + 2] = view.LengthInBytes;
        result[index * 7 + 3] = view.Extent.X;
        result[index * 7 + 4] = view.IsValid ? 1 : 0;
        result[index * 7 + 5] = view.SubView(0, 0).Length;
        result[index * 7 + 6] = view.Cast<long>().Length;
    }

    /// <summary>
    /// Stores the members of a default view.
    /// </summary>
    public static void DefaultViewMembersKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        ArrayView<int> view = default;
        result[index * 7 + 0] = view.IntLength;
        result[index * 7 + 1] = view.Length;
        result[index * 7 + 2] = view.LengthInBytes;
        result[index * 7 + 3] = view.Extent.X;
        result[index * 7 + 4] = view.IsValid ? 1 : 0;
        result[index * 7 + 5] = view.SubView(0, 0).Length;
        result[index * 7 + 6] = view.Cast<long>().Length;
    }

    /// <summary>
    /// Reads an element of the empty view behind a length check, which keeps
    /// an element access on the null view in the generated code.
    /// </summary>
    public static void EmptyViewElementKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        var view = ArrayView<long>.Empty;
        if (view.Length > index)
            result[index] = view[index];
        else
            result[index] = 7;
    }
}
