// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpRadixSortHalfRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WarpRadixSortHalfRuntime
// Warp radix sorts of Half values order negative values by magnitude before
// positive ones, in ascending and descending order. The 8 real values are
// padded to the warp size with values that sort to the end.
// Expected output: the ascending, then the descending order of the inputs.

using System;
using ILGPU;
using ILGPU.RadixSort;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpRadixSortHalfRuntimeAscendingKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> input,
        ArrayView1D<Half, Stride1D.Dense> output)
    {
        Half value = input[index];
        Half sorted = Warp.RadixSort<Half, AscendingHalf>(value);
        output[index] = sorted;
    }

    public static void WarpRadixSortHalfRuntimeDescendingKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> input,
        ArrayView1D<Half, Stride1D.Dense> output)
    {
        Half value = input[index];
        Half sorted = Warp.RadixSort<Half, DescendingHalf>(value);
        output[index] = sorted;
    }
}

static class Program
{
    static readonly Half[] Values =
    [
        (Half)3.5f, (Half)(-1.0f), Half.NegativeZero, (Half)(-100.25f),
        (Half)2.0f, (Half)(-2.0f), (Half)(-0.5f), (Half)7.0f,
    ];

    static Half[] Pad(Half padding, int ws)
    {
        var data = new Half[ws];
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
        using var ascInput = stream.Allocate1D(Pad(Half.PositiveInfinity, ws));
        using var descInput = stream.Allocate1D(Pad(Half.NegativeInfinity, ws));
        using var ascOutput = stream.Allocate1D<Half>(ws);
        using var descOutput = stream.Allocate1D<Half>(ws);
        stream.Launch(
            (Index1D)ws,
            index => Kernels.WarpRadixSortHalfRuntimeAscendingKernel(
                index, ascInput.View, ascOutput.View));
        stream.Launch(
            (Index1D)ws,
            index => Kernels.WarpRadixSortHalfRuntimeDescendingKernel(
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
