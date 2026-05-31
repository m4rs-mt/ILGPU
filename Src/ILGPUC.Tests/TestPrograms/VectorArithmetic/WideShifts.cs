// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WideShifts.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: WideShifts
// Shifts of 32-bit and 64-bit values by per-lane int amounts, including
// amounts beyond the width of the shifted type.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x, int u)
    {
        return (x << (u + x + 20)) + ((long)x << (u + x + 50))
            + ((uint)(x + 1) >> (x + 28)) + (-x >> (x + 30));
    }

    public static void WideShiftsKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output, int u)
    {
        output[index] = Compute(index.X % 9, u);
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
        stream.Launch((Index1D)12, index => Kernels.WideShiftsKernel(index, output.View, 10));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
