// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerLaneViewLength.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PerLaneViewLength
// Each lane selects a sub-view of a different length by its lane index and
// reads its Length, IntLength and last element: even lanes data[0..3),
// odd lanes data[3..8), with data[i] = i + 1.
// Expected output: 3303 5508 3303 5508

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PerLaneViewLengthKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        ArrayView<int> view = lane % 2 == 0
            ? data.BaseView.SubView(0, 3)
            : data.BaseView.SubView(3, 5);
        output[lane] = view.IntLength * 1000 + (int)view.Length * 100
            + view[view.IntLength - 1];
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
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.PerLaneViewLengthKernel(index, data.View, output.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
    }
}
