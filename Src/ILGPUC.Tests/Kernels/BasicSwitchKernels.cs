// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicSwitchKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for switch statement tests.
/// </summary>
static class BasicSwitchKernels
{
    /// <summary>
    /// Switch that maps input values to different outputs.
    /// </summary>
    public static void SwitchMapKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int result;
        switch (value)
        {
            case 0:
                result = 10;
                break;
            case 1:
                result = 20;
                break;
            case 2:
                result = 30;
                break;
            case 3:
                result = 40;
                break;
            default:
                result = -1;
                break;
        }
        data[index] = result;
    }

    /// <summary>
    /// Switch that stores different computed values.
    /// </summary>
    public static void SwitchStoreKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int selector)
    {
        switch (selector)
        {
            case 0:
                data[index] = index.X * 2;
                break;
            case 1:
                data[index] = index.X + 100;
                break;
            case 2:
                data[index] = index.X * index.X;
                break;
            default:
                data[index] = 0;
                break;
        }
    }
}
