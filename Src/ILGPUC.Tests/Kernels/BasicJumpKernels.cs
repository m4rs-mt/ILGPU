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
}
