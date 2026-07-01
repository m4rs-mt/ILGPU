// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WhileLoadSearch.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: WhileLoadSearch
// Every lane searches an input array for a value with a while loop whose
// condition loads the current element.
// Expected output: the index of the value per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WhileLoadSearchKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> output,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int target = index.X * 13 % 101;
        int k = 0;
        while (data[k] != target)
            k++;
        output[index] = k;
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
        stream.Launch((Index1D)12, index => Kernels.WhileLoadSearchKernel(index, output.View, data.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
