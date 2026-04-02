// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LambdaConditional.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: LambdaConditional
// Lambda clamps the thread index to threshold=2.
// Expected output: 0 1 2 2 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LambdaConditionalKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int threshold)
    {
        int t = threshold;
        Func<int, int> clamp = x => x > t ? t : x;
        data[index] = clamp(index.X);
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.LambdaConditionalKernel(index, buffer.View, 2));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
