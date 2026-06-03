// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IntRotateLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: IntRotateLoop
// Three loop-carried values rotate on every iteration.
// With 4 threads: input = [1,2,3,4], thread t sums the first value of
// iterations 0..t, which cycles through input[0], input[1] and input[2]
// Expected output: 1 3 6 7

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IntRotateLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int a = input[0];
        int b = input[1];
        int c = input[2];
        int sum = 0;
        for (int i = 0; i <= index.X; ++i)
        {
            sum += a;
            int t = a;
            a = b;
            b = c;
            c = t;
        }
        data[index] = sum;
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

        using var input = stream.Allocate1D<int>(4);
        input.View.CopyFromCPU(stream, new int[] { 1, 2, 3, 4 });
        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.IntRotateLoopKernel(index, input.View, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
