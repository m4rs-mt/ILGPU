// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SimpleArray.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SimpleArray
// Kernel reads input, computes value*2+1, writes output.
// Input: [1, 2, 3, 4] → Output: [3, 5, 7, 9]
// Expected output: 3 5 7 9

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SimpleArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        output[index] = value * 2 + 1;
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

        int[] inputData = [1, 2, 3, 4];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.SimpleArrayKernel(index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
