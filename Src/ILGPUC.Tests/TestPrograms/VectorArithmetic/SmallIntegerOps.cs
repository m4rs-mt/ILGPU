// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SmallIntegerOps.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: SmallIntegerOps
// Signed and unsigned 8-bit and 16-bit divisions and comparisons.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x)
    {
        byte b = (byte)(x * 37);
        sbyte sb = (sbyte)(x * 37);
        short sh = (short)(x * 5000);
        ushort us = (ushort)(x * 9000);
        return b / 3 + sb / 3 * 10 + sh / 7 * 100 + us / 7 * 1000
            + (sb < 0 ? 1 : 0) + (us > 40000 ? 2 : 0);
    }

    public static void SmallIntegerOpsKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output)
    {
        output[index] = Compute(index.X % 9);
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
        stream.Launch((Index1D)12, index => Kernels.SmallIntegerOpsKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
