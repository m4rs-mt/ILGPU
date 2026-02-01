// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivergentLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DivergentLoop
// Kernel loops index-dependent number of times, summing loop counter.
// Expected output: 0 1 3 6

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DivergentLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int iterations = index.X % 4 + 1;
        int sum = 0;
        for (int i = 0; i < iterations; i++)
        {
            sum += i;
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.DivergentLoopKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
