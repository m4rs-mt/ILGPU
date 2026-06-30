// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedLoopContinueOuter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: NestedLoopContinueOuter
// Lanes jump from an inner loop to the end of the outer loop body in
// different iterations, skipping the code after the inner loop.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x)
    {
        long s = 0;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                if (i + j > x)
                    goto next;
                s += j + 1;
            }
            s += 100;
        next:
            s += 1000 * (i + 1);
        }
        return s;
    }

    public static void NestedLoopContinueOuterKernel(
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
        stream.Launch((Index1D)12, index => Kernels.NestedLoopContinueOuterKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
