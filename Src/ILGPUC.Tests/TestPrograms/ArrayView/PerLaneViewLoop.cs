// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerLaneViewLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PerLaneViewLoop
// Each lane shrinks a view by one element per loop iteration, running t % 4
// iterations, so the lanes leave the loop with different views. Lane t then
// outputs 100 * length + first element, with data[i] = i + 1.
// Expected output: 801 702 603 504 801 702 603 504

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PerLaneViewLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        var view = data;
        for (int i = 0; i < lane % 4; i++)
            view = view.SubView(1, view.IntLength - 1);
        output[lane] = view.IntLength * 100 + view[0];
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

        var values = new int[8];
        for (int i = 0; i < values.Length; i++)
            values[i] = i + 1;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(8);
        stream.Launch((Index1D)8, index => Kernels.PerLaneViewLoopKernel(index, data.View, output.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
    }
}
