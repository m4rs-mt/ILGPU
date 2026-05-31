// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivisorReuse.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: DivisorReuse
// A divisor divided by in a divergent branch is used again after the branch
// by all lanes, including the lanes that skipped the division.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x)
    {
        int b = x + 5;
        int q = 0;
        if (x > 3)
            q = 100 / b;
        return q * 1000 + b;
    }

    public static void DivisorReuseKernel(
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
        stream.Launch((Index1D)12, index => Kernels.DivisorReuseKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
