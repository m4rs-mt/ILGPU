// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FixedBufferKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Fixed-size buffer operations.
/// </summary>
static class FixedBufferKernels
{
    public unsafe struct FixedBuffer4
    {
        public fixed int Data[4];
    }

    public static void FixedReadKernel(
        Index1D index,
        ArrayView1D<FixedBuffer4, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        unsafe
        {
            FixedBuffer4 buf = input[index];
            output[index] = buf.Data[0] + buf.Data[1] + buf.Data[2] + buf.Data[3];
        }
    }

    public static void FixedWriteKernel(
        Index1D index,
        ArrayView1D<FixedBuffer4, Stride1D.Dense> output,
        int value)
    {
        unsafe
        {
            FixedBuffer4 buf;
            buf.Data[0] = value;
            buf.Data[1] = value * 2;
            buf.Data[2] = value * 3;
            buf.Data[3] = value * 4;
            output[index] = buf;
        }
    }
}
