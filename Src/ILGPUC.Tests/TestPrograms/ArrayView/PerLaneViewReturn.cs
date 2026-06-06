// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerLaneViewReturn.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PerLaneViewReturn
// A helper method returns one of two view parameters depending on the lane
// index, and the lane loads and stores through the returned view: even lanes
// use a = data[0..8), odd lanes b = data[8..16), with data[i] = 3 * i.
// Lane t reads element t / 2 and writes 100 + t to element t / 2 + 4.
// Expected output: 0 24 3 27 6 30 9 33 / 0 3 6 9 100 102 104 106 24 27 30 33 101 103 105 107

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static ArrayView1D<int, Stride1D.Dense> Pick(
        ArrayView1D<int, Stride1D.Dense> a,
        ArrayView1D<int, Stride1D.Dense> b,
        int lane) =>
        lane % 2 == 0 ? a : b;

    public static void PerLaneViewReturnKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> a,
        ArrayView1D<int, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        var view = Pick(a, b, lane);
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
        stream.Launch((Index1D)8, index => Kernels.PerLaneViewReturnKernel(
            index, data.View.SubView(0, 8), data.View.SubView(8, 8), output.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
        Console.WriteLine(string.Join(" ", data.GetAsArray1D()));
    }
}
