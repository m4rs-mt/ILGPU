// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupRadixSortFloatRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupRadixSortFloatRuntime
// Input read from a runtime buffer so the optimizer cannot fold.
// 8 threads in one group (matches CPU VectorWidth=8).
// Input = [3.5, -1, -0, -100.25, 2, -2, -0.5, 7]
// Group.RadixSort<float, AscendingFloat> sorts within the group, ordering
// negative values by magnitude before positive ones.
// Expected output: -100.25 -2 -1 -0.5 -0 2 3.5 7

using System;
using ILGPU;
using ILGPU.RadixSort;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupRadixSortFloatRuntimeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> input,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        float value = input[index];
        float sorted = Group.RadixSort<float, AscendingFloat>(value);
        output[index] = sorted;
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

        float[] inputData = [3.5f, -1.0f, -0.0f, -100.25f, 2.0f, -2.0f, -0.5f, 7.0f];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<float>(8);
        stream.Launch(
            (Index1D)8,
            index => Kernels.GroupRadixSortFloatRuntimeKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
