// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicPhiKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for phi node / value merging tests.
/// </summary>
static class BasicPhiKernels
{
    /// <summary>
    /// Value merging from inlined branches (phi node at join point).
    /// </summary>
    public static void PhiInliningKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b)
    {
        int x;
        if (a > b)
            x = a - b;
        else
            x = b - a;

        // x is a phi: either (a - b) or (b - a)
        data[index] = x;
    }

    /// <summary>
    /// Deep nesting requiring multiple phi nodes.
    /// </summary>
    public static void DeepPhiKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int result;
        if (value > 100)
        {
            if (value > 200)
            {
                if (value > 300)
                    result = 4;
                else
                    result = 3;
            }
            else
            {
                result = 2;
            }
        }
        else
        {
            result = 1;
        }

        // result merges from 4 possible branches
        data[index] = result;
    }
}
