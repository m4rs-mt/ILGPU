// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullArrayKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for null arrays and their null tests.
/// </summary>
static class NullArrayKernels
{
    private struct ArrayHolder
    {
        public int[]? Values;
    }

    private static int LengthOrMinusOne(int[]? values) =>
        values == null ? -1 : values.Length;

    private static bool IsNotNull(int[]? values) => values != null;

    /// <summary>
    /// Assign null to a local array and test it with ==, !=, is and is not.
    /// </summary>
    public static void NullCheckKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[]? a = null;
        if (index.X > 1)
            a = new int[2];
        int r = 0;
        if (a != null) r += 1;
        if (a is null) r += 10;
        if (a is not null) r += 100;
        output[index] = r;
    }

    /// <summary>
    /// Pass null, an empty array and an allocated array to helpers that test
    /// their argument for null, by a branch and by cgt.un.
    /// </summary>
    public static void NullArgumentKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = LengthOrMinusOne(null) * 100 +
            LengthOrMinusOne(new int[0]) * 10 +
            LengthOrMinusOne(new int[3]) +
            (IsNotNull(new int[2]) ? 1000 : 0) +
            (IsNotNull(null) ? 10000 : 0);
    }

    /// <summary>
    /// Test the unassigned (null) array field of a default structure.
    /// </summary>
    public static void DefaultFieldKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        ArrayHolder holder = default;
        if (index.X > 1)
            holder.Values = new int[4];
        output[index] = holder.Values is null ? -1 : holder.Values.Length;
    }

    /// <summary>
    /// Read the length of null itself (ldnull; ldlen), which is 0 in kernels.
    /// </summary>
    public static void NullLengthKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = ((int[]?)null)!.Length + index.X;
    }

    /// <summary>
    /// Replace a null local with a new array in the first iteration of a loop,
    /// which joins the null array and the new array in the loop header.
    /// </summary>
    public static void AssignInLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[]? a = null;
        int s = 0;
        for (int k = 0; k < 3; ++k)
        {
            if (a == null)
                a = new int[3];
            a[k] = k + index.X;
            s += a.Length;
        }
        output[index] = s * 100 + a![2];
    }
}
