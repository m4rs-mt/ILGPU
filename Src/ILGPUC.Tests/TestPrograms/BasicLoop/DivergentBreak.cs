// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivergentBreak.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DivergentBreak
// Each lane leaves the loop through the break at a different iteration, so the
// value merged at the loop exit differs per lane.
// Expected output: 1 6 10 10 15 21 21 28

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DivergentBreakKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += i;
            if (sum > index.X * 3)
                break;
        }
        data[index] = sum;
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

        using var buffer = stream.Allocate1D<int>(8);
        stream.Launch((Index1D)8, index => Kernels.DivergentBreakKernel(index, buffer.View));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
