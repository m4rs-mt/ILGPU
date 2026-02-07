// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicLoopKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for loop construct tests.
/// </summary>
static class BasicLoopKernels
{
    /// <summary>
    /// While loop: sum 0..9 into data[index].
    /// </summary>
    public static void WhileLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        int i = 0;
        while (i < 10)
        {
            sum += i;
            i++;
        }
        data[index] = sum;
    }

    /// <summary>
    /// For loop: sum 1..n into data[index].
    /// </summary>
    public static void ForLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int n)
    {
        int sum = 0;
        for (int i = 1; i <= n; i++)
        {
            sum += i;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Do-while loop: count down from value, store iteration count.
    /// </summary>
    public static void DoWhileLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int count = 0;
        int v = value;
        do
        {
            count++;
            v--;
        } while (v > 0);
        data[index] = count;
    }

    /// <summary>
    /// Nested loop: for i in 0..3, j in 0..3, accumulate i * 4 + j.
    /// </summary>
    public static void NestedLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                sum += i * 4 + j;
            }
        }
        data[index] = sum;
    }

    /// <summary>
    /// Divergent loop: loop count depends on index value (divergent control flow).
    /// </summary>
    public static void DivergentLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int iterations = index.X % 4 + 1;
        int sum = 0;
        for (int i = 0; i < iterations; i++)
        {
            sum += i;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Loop with break: break when accumulator exceeds 10.
    /// </summary>
    public static void LoopWithBreakKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += i;
            if (sum > 10)
                break;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Loop with continue: skip even numbers, sum only odd values 0..9.
    /// </summary>
    public static void LoopWithContinueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
                continue;
            sum += i;
        }
        data[index] = sum;
    }
}
