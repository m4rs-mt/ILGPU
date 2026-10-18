// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullPointerLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: NullPointerLoop
// A pointer alternates between two local variables in a loop until it becomes null,
// after a number of iterations that differs per thread.
// With 4 threads: x = t + 1, y = 10 * (t + 1); thread t reads x, y, x, ... t + 1 times
// Expected output: 1 22 36 88

using System;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    public static void NullPointerLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = index + 1;
        int y = 10 * (index + 1);
        int* p = &x;
        int sum = 0;
        for (int i = 0; p != null; ++i)
        {
            sum += *p;
            p = i == index ? null : p == &x ? &y : &x;
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4, index => Kernels.NullPointerLoopKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
