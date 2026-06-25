// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerLaneSubViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PerLaneSubViews
// Each lane selects one of two sub-views of a buffer by its lane index, then
// loads and stores through the selected view: even lanes use data[0..8),
// odd lanes data[8..16), with data[i] = 3 * i. Lane t reads element t / 2
// and writes 100 + t to element t / 2 + 4 of its view.
// Expected output: 0 24 3 27 6 30 9 33 / 0 3 6 9 100 102 104 106 24 27 30 33 101 103 105 107

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PerLaneSubViewsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        ArrayView<int> view = lane % 2 == 0
            ? data.BaseView.SubView(0, 8)
            : data.BaseView.SubView(8, 8);
        output[lane] = view[lane / 2];
        view[lane / 2 + 4] = 100 + lane;
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
            values[i] = 3 * i;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(8);
        stream.Launch((Index1D)8, index => Kernels.PerLaneSubViewsKernel(index, data.View, output.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
        Console.WriteLine(string.Join(" ", data.GetAsArray1D()));
    }
}
