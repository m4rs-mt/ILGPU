// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoopSearchBreakValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: LoopSearchBreakValue
// A search loop that leaves with the found position, or with a default
// after the last iteration; both exits merge into code that uses the value.
// Each lane searches from a different start; twelve lanes span two SIMD
// groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LoopSearchBreakValueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int x = index.X % 9;
        int r = -1;
        int sum = 0;
        for (int i = 0; i < 8; i++)
        {
            int v = (i * 5 + x * 3) % 11;
            if (v > 8)
            {
                r = i * 3 + x;
                break;
            }
            sum += v;
        }
        if (r < 0)
            r = sum + 50;
        output[index] = r * 10 + x;
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

        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.LoopSearchBreakValueKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
