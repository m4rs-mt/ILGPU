// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupRadixSortAscendingInt32Runtime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupRadixSortAscendingInt32Runtime
// Input read from a runtime buffer so the optimizer cannot fold.
// 8 threads in one group (matches CPU VectorWidth=8).
// Input = [7, 3, 5, 1, 8, 2, 6, 4]
// Group.RadixSort<int, AscendingInt32> sorts within the group.
// Expected output: 1 2 3 4 5 6 7 8

using System;
using ILGPU;
using ILGPU.RadixSort;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupRadixSortAscendingInt32RuntimeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        int sorted = Group.RadixSort<int, AscendingInt32>(value);
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

        int[] inputData = [7, 3, 5, 1, 8, 2, 6, 4];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(8);
        stream.Launch(
            (Index1D)8,
            index => Kernels.GroupRadixSortAscendingInt32RuntimeKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
