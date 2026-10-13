// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpRadixSortFloatRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WarpRadixSortFloatRuntime
// Warp radix sorts of float values order negative values by magnitude before
// positive ones, in ascending and descending order. The 8 real values are
// padded to the warp size with values that sort to the end.
// Expected output: the ascending, then the descending order of the inputs.

using System;
using ILGPU;
using ILGPU.RadixSort;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpRadixSortFloatRuntimeAscendingKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> input,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        float value = input[index];
        float sorted = Warp.RadixSort<float, AscendingFloat>(value);
        output[index] = sorted;
    }

    public static void WarpRadixSortFloatRuntimeDescendingKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> input,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        float value = input[index];
        float sorted = Warp.RadixSort<float, DescendingFloat>(value);
        output[index] = sorted;
    }
}

static class Program
{
    static readonly float[] Values =
        [3.5f, -1.0f, -0.0f, -100.25f, 2.0f, -2.0f, -0.5f, 7.0f];

    static float[] Pad(float padding, int ws)
    {
        var data = new float[ws];
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
        using var ascInput = stream.Allocate1D(Pad(float.PositiveInfinity, ws));
        using var descInput = stream.Allocate1D(Pad(float.NegativeInfinity, ws));
        using var ascOutput = stream.Allocate1D<float>(ws);
        using var descOutput = stream.Allocate1D<float>(ws);
        stream.Launch(
            (Index1D)ws,
            index => Kernels.WarpRadixSortFloatRuntimeAscendingKernel(
                index, ascInput.View, ascOutput.View));
        stream.Launch(
            (Index1D)ws,
            index => Kernels.WarpRadixSortFloatRuntimeDescendingKernel(
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
