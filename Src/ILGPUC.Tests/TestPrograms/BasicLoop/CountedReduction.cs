// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CountedReduction.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: CountedReduction
// Every lane sums a strided walk over an input array with a loop whose trip
// count is a kernel argument: a counted loop with a load per iteration.
// Expected output: one sum per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void CountedReductionKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> output,
        ArrayView1D<int, Stride1D.Dense> data,
        int n)
    {
        long sum = 0;
        for (int k = 0; k < n; k++)
            sum += data[(k * 7 + index.X) % n] * (k & 3);
        output[index] = sum;
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

        var values = new int[1000];
        for (int i = 0; i < values.Length; i++)
            values[i] = i * 37 % 101;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<long>(12);
        stream.Launch((Index1D)12, index => Kernels.CountedReductionKernel(index, output.View, data.View, values.Length));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
