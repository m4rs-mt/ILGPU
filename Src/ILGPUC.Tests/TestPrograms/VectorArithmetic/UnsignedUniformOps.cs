// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnsignedUniformOps.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: UnsignedUniformOps
// Unsigned 32-bit operations on a uniform kernel parameter.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x, int w)
    {
        uint a = (uint)w;
        long r = (long)(a / 3u) + (a >> 5) + (a > 5u ? 1 : 0);
        return r + x;
    }

    public static void UnsignedUniformOpsKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output, int w)
    {
        output[index] = Compute(index.X % 9, w);
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var output = stream.Allocate1D<long>(12);
        stream.Launch((Index1D)12, index => Kernels.UnsignedUniformOpsKernel(index, output.View, -1));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
