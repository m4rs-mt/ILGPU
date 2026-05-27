// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpShuffleLong.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: WarpShuffleLong
// Warp shuffles of 64-bit values move both 32-bit halves.
// 4 threads, v[i] = (i + 1) << 32 | (i + 10); out[i] = Shuffle(v, (i + 1) % 4).
// Expected output: 8589934603 12884901900 17179869197 4294967306

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpShuffleLongKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        long value = data[index];
        output[index] = Warp.Shuffle(value, (Warp.LaneIndex + 1) % 4);
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

        var values = new long[4];
        for (int i = 0; i < values.Length; i++)
            values[i] = ((long)(i + 1) << 32) | (long)(i + 10);
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<long>(4);
        stream.Launch((Index1D)4, index => Kernels.WarpShuffleLongKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
