// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicIfKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for conditional (if/else) tests.
/// </summary>
static class BasicIfKernels
{
    public static void IfTrueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = true ? 42 : 23;
    }

    public static void IfFalseKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = false ? 42 : 23;
    }

    public static void IfSideEffectsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int threshold)
    {
        data[index] = index.X > threshold ? 1 : 0;
    }

    public static void IfAndOrKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int a, int b)
    {
        if (a > 0 && b > 0)
            data[index] = 1;
        else if (a > 0 || b > 0)
            data[index] = 2;
        else
            data[index] = 0;
    }

    public static void NestedIfKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        if (value > 10)
        {
            if (value > 20)
                data[index] = 3;
            else
                data[index] = 2;
        }
        else
        {
            data[index] = 1;
        }
    }

    public static void IfChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        if (value == 0)
            data[index] = 10;
        else if (value == 1)
            data[index] = 20;
        else if (value == 2)
            data[index] = 30;
        else
            data[index] = 40;
    }
}
