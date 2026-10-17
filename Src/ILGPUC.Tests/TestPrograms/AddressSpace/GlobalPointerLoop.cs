// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GlobalPointerLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: GlobalPointerLoop
// A reference advances through global memory in a loop whose trip count differs
// per thread (a loop-carried pointer that diverges across threads).
// With 4 threads: input = [1,2,3,4], thread t sums input[0..t]
// Expected output: 1 3 6 10

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GlobalPointerLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        ref int current = ref input[0];
        int sum = 0;
        for (int i = 0; i <= index.X; ++i)
        {
            sum += current;
            current = ref Unsafe.Add(ref current, 1);
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
        stream.Launch((Index1D)4, index => Kernels.GlobalPointerLoopKernel(index, input.View, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
