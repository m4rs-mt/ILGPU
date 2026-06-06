// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ViewShrinkLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ViewShrinkLoop
// A loop sums the first element of a contiguous view of a kernel parameter and
// drops that element from the view on every iteration.
// With 4 threads: data[i] = 3 * i + 1 for i < 16 and thread t runs t + 2
// iterations, which sums data[0..t + 2)
// Expected output: 5 12 22 35

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ViewShrinkLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        var v = data.BaseView;
        int s = 0;
        for (int k = 0; k < index.X + 2; k++)
        {
            s += v[0];
            v = v.SubView(1);
        }
        output[index] = s;
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
            values[i] = 3 * i + 1;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.ViewShrinkLoopKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
