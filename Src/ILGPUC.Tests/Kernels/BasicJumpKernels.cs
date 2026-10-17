// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicJumpKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for jump/goto tests.
/// </summary>
static class BasicJumpKernels
{
    /// <summary>
    /// Simple goto: jump over an assignment.
    /// </summary>
    public static void GotoKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int result = 0;
        if (value > 5)
            goto done;
        result = value * 2;
    done:
        data[index] = result;
    }

    /// <summary>
    /// Nested labels with jumps: multiple goto targets.
    /// </summary>
    public static void NestedLabelKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int result = 0;
        if (value == 0)
            goto zero;
        if (value == 1)
            goto one;
        result = value * 3;
        goto done;
    zero:
        result = 100;
        goto done;
    one:
        result = 200;
    done:
        data[index] = result;
    }

    /// <summary>
    /// Irreducible loop written with gotos directly in the kernel body: the
    /// two labels of the loop can both be entered from outside of it.
    /// </summary>
    public static void IrreducibleGotoKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = index.X + 1;
        int y = 0;
        if (index.X % 3 == 0)
            goto second;
    first:
        x = x * 3 + 1;
        y++;
        if (x % 7 == 0)
            goto done;
    second:
        x = x / 2 + y;
        y += 2;
        if (y < 12)
            goto first;
    done:
        data[index] = x * 100 + y;
    }

    /// <summary>
    /// Three nested loops left with a goto out of all loops, a goto to the
    /// end of the outer loop body and a break out of the inner loop.
    /// </summary>
    public static void NestedLoopGotoKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int a, b = 0, c = 0, r = 0;
        for (a = 0; a < 4; a++)
        {
            for (b = 0; b < 4; b++)
            {
                for (c = 0; c < 4; c++)
                {
                    int v = input[a * 16 + b * 4 + c];
                    if (v == index.X)
                        goto done;
                    if (v > 40)
                        goto nextA;
                    if (v < -40)
                        break;
                    r += v;
                }
                r ^= b;
            }
        nextA:
            r += 1000;
        }
    done:
        data[index] = r * 7 + a * 100 + b * 10 + c;
    }
}
