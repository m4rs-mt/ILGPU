// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ViewSwapLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ViewSwapLoop
// A loop swaps two sub-views of a kernel parameter on every iteration, once
// with ArrayView1D views and once with contiguous ArrayView views.
// With 4 threads: data[i] = (7 * i) % 11 - 5 for i < 16, x and y are the
// first and second half of data, thread t runs t + 2 iterations and computes
// s = 3 * s + x[k + t] before swapping x and y, and writes s + x[0]
// Expected output: -17 16 -103 443 -17 16 -103 443

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ViewSwapLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        var x = data.SubView(0, 8);
        var y = data.SubView(8, 8);
        int s = 0;
        for (int k = 0; k < lane + 2; k++)
        {
            s = s * 3 + x[k + lane];
            var t = x;
            x = y;
            y = t;
        }
        output[index] = s + x[0];
    }

    public static void ContiguousViewSwapLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        var x = data.AsContiguous().SubView(0, 8);
        var y = data.AsContiguous().SubView(8, 8);
        int s = 0;
        for (int k = 0; k < lane + 2; k++)
        {
            s = s * 3 + x[k + lane];
            var t = x;
            x = y;
            y = t;
        }
        output[index] = s + x[0];
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

        var values = new int[16];
        for (int i = 0; i < values.Length; i++)
            values[i] = (7 * i) % 11 - 5;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(4);
        using var contiguousOutput = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.ViewSwapLoopKernel(index, data.View, output.View));
        stream.Launch((Index1D)4, index => Kernels.ContiguousViewSwapLoopKernel(index, data.View, contiguousOutput.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in contiguousOutput.GetAsArray1D())
            Console.WriteLine(v);
    }
}
