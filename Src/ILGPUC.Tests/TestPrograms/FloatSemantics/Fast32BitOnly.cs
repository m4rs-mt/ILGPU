// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Fast32BitOnly.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: Fast32BitOnly
// Double arithmetic compiled with MathMode.Fast32BitOnly runs in float.
// Inputs: 2, 4, 6, 8 -> x * 0.5 + 1.0
// Expected output: 2 3 4 5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Fast32BitOnlyKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        float x = data[index];
        output[index] = (float)((double)x * 0.5 + 1.0);
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

        using var data = stream.Allocate1D(new float[] { 2.0f, 4.0f, 6.0f, 8.0f });
        using var output = stream.Allocate1D<float>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.Fast32BitOnlyKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
