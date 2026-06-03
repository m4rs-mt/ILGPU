// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DoWhileRotateLoad.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DoWhileRotateLoad
// A do-while loop whose body is a single block rotates three loop-carried
// values and loads from a view on every iteration.
// With 4 threads: input = [1,2,3,4,5,6], thread t runs t+3 iterations, and
// c receives the old a plus input[k] in iteration k
// Expected output: 20406 40606 60609 60912

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DoWhileRotateLoadKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int a = 1;
        int b = 2;
        int c = 3;
        int k = 0;
        do
        {
            int t = a;
            a = b;
            b = c;
            c = t + input[k];
            k++;
        }
        while (k < index.X + 3);
        data[index] = a * 10000 + b * 100 + c;
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

        using var input = stream.Allocate1D<int>(6);
        input.View.CopyFromCPU(stream, new int[] { 1, 2, 3, 4, 5, 6 });
        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.DoWhileRotateLoadKernel(index, input.View, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
