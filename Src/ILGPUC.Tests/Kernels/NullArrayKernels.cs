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
using System.Diagnostics.CodeAnalysis;

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

    private static int[]? AllocateOrNull(int x) => x > 1 ? new int[4] : null;

    private static void Ensure([NotNull] ref int[]? values)
    {
        values ??= new int[3];
        values[0] = 1;
    }

    /// <summary>
    /// Merge an allocated array with null and test the result for null.
    /// </summary>
    public static void ConditionalNullKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[]? a = (index.X & 1) == 0 ? new int[3] : null;
        output[index] = a == null ? -1 : a.Length;
    }

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
    /// Replace a null array with ??.
    /// </summary>
    public static void CoalesceKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[]? a = index.X > 1 ? new int[2] : null;
        int[] b = a ?? new int[5];
        output[index] = b.Length;
    }

    /// <summary>
    /// Read the length of an array that may be null with ?.Length.
    /// </summary>
    public static void ConditionalLengthKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[]? a = index.X > 1 ? new int[2] : null;
        output[index] = a?.Length ?? -1;
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
    /// Compare arrays by reference, also as objects of different types.
    /// </summary>
    public static void ReferenceEqualsKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] a = new int[2];
        int[] b = new int[2];
        int[] c = index.X > 1 ? a : b;
        float[]? f = index.X > 2 ? new float[2] : null;
        output[index] = (c == a ? 1 : 0) + (c == b ? 10 : 0) +
            (ReferenceEquals(c, null) ? 100 : 0) +
            ((object?)c == f ? 1000 : 0);
    }

    /// <summary>
    /// Return null from a helper and test the result with a property pattern
    /// and ?[].
    /// </summary>
    public static void ReturnNullKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[]? a = AllocateOrNull(index.X & 3);
        output[index] = a is { Length: > 2 } ? 1 : a?[0] ?? -1;
    }

    /// <summary>
    /// Replace null arrays with ??= in a structure field and through a ref
    /// parameter, and store null to a field.
    /// </summary>
    public static void CoalesceAssignKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        ArrayHolder holder = default;
        if (index.X > 1)
            holder.Values = new int[2];
        holder.Values ??= new int[3];
        int[] b = holder.Values;
        holder.Values = null;
        int[]? c = index.X > 2 ? b : null;
        Ensure(ref c);
        output[index] = b.Length + (holder.Values == null ? 10 : 0) +
            c.Length * 100 + c[0];
    }

    /// <summary>
    /// Replace a conditional null array with ?? while another conditional null
    /// array stays on the IL stack, and test the result for null.
    /// </summary>
    public static void CoalesceOnStackKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = ((index.X > 1 ? new int[2] : null) ??
            (index.X < 1 ? new int[1] : null)) == null ? -1 : 1;
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
