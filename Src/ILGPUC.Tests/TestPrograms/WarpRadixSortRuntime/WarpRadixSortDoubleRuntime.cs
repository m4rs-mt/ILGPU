// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpRadixSortDoubleRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WarpRadixSortDoubleRuntime
// Warp radix sorts of double values order negative values by magnitude before
// positive ones, in ascending and descending order. The 8 real values are
// padded to the warp size with values that sort to the end.
// Expected output: the ascending, then the descending order of the inputs.

using System;
using ILGPU;
using ILGPU.RadixSort;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpRadixSortDoubleRuntimeAscendingKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> input,
        ArrayView1D<double, Stride1D.Dense> output)
    {
        double value = input[index];
        double sorted = Warp.RadixSort<double, AscendingDouble>(value);
        output[index] = sorted;
    }

    public static void WarpRadixSortDoubleRuntimeDescendingKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> input,
        ArrayView1D<double, Stride1D.Dense> output)
    {
        double value = input[index];
        double sorted = Warp.RadixSort<double, DescendingDouble>(value);
        output[index] = sorted;
    }
}

static class Program
{
    static readonly double[] Values =
        [3.5, -1.0, -0.0, -100.25, 2.0, -2.0, -0.5, 7.0];

    static double[] Pad(double padding, int ws)
    {
        var data = new double[ws];
        for (int i = 0; i < ws; i++)
            data[i] = i < Values.Length ? Values[i] : padding;
        return data;
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        int ws = Math.Max(accelerator.WarpSize, 8);
        using var ascInput = stream.Allocate1D(Pad(double.PositiveInfinity, ws));
        using var descInput = stream.Allocate1D(Pad(double.NegativeInfinity, ws));
        using var ascOutput = stream.Allocate1D<double>(ws);
        using var descOutput = stream.Allocate1D<double>(ws);
        stream.Launch(
            (Index1D)ws,
            index => Kernels.WarpRadixSortDoubleRuntimeAscendingKernel(
                index, ascInput.View, ascOutput.View));
        stream.Launch(
            (Index1D)ws,
            index => Kernels.WarpRadixSortDoubleRuntimeDescendingKernel(
                index, descInput.View, descOutput.View));
        stream.Synchronize();

        var asc = ascOutput.GetAsArray1D();
        for (int i = 0; i < Values.Length; i++)
            Console.WriteLine(asc[i]);
        var desc = descOutput.GetAsArray1D();
        for (int i = 0; i < Values.Length; i++)
            Console.WriteLine(desc[i]);
    }
}
