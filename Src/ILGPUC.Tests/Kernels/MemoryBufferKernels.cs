// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MemoryBufferKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Memory buffer copy and transform kernels.
/// </summary>
static class MemoryBufferKernels
{
    public static void CopyKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = data[index];
    }

    public static void ScaleKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output,
        int scale)
    {
        output[index] = input[index] * scale;
    }
}
