// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerLaneLocalArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PerLaneLocalArrays
// Each lane picks one of two local arrays through a branch on its lane index
// and reads back the element it wrote: even lanes 5 + data[t] + t,
// odd lanes 7 + data[t + 1] + t, with data[i] = 10 * i.
// Expected output: 5 28 27 50 49 72 71 94

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PerLaneLocalArraysKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        int[] a;
        if (lane % 2 == 0)
        {
            a = new int[4];
            a[1] = 5 + data[lane];
        }
        else
        {
            a = new int[4];
            a[1] = 7 + data[lane + 1];
        }
        output[index] = a[1] + lane;
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

        var values = new int[9];
        for (int i = 0; i < values.Length; i++)
            values[i] = 10 * i;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(8);
        stream.Launch((Index1D)8, index => Kernels.PerLaneLocalArraysKernel(index, data.View, output.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
    }
}
