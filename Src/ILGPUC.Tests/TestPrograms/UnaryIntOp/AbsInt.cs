// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AbsInt.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: AbsInt
// Kernel computes absolute value of integers.
// Input: [-3,-1,0,5], Expected output: 3 1 0 5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AbsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = input[index] < 0 ? -input[index] : input[index];
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

        var inputData = new int[] { -3, -1, 0, 5 };
        using var inputBuf = stream.Allocate1D(inputData);
        using var resultBuf = stream.Allocate1D<int>(4);

        stream.Launch((Index1D)4, index => Kernels.AbsKernel(index, inputBuf.View, resultBuf.View));
        stream.Synchronize();

        var data = resultBuf.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
