// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AllocateInHelperInLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: AllocateInHelperInLoop
// A helper allocates a local array, updates it with compound assignments and
// is called in every iteration of a loop, so every call must start from a
// zeroed array, also after the helper is inlined into the loop.
// With 4 threads and 6 iterations: the elements of the array sum up to i + 3
// in iteration i, and every thread folds these sums into s = s * 3 + sum.
// Expected output: 1271 1271 1271 1271

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Step(int i)
    {
        int[] a = new int[4];
        a[i % 4] += i + 1;
        a[(i + 1) % 4] += 2;
        return a[0] + a[1] + a[2] + a[3];
    }

    public static void AllocateInHelperInLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int count)
    {
        int s = 0;
        for (int i = 0; i < count; i++)
        {
            s = s * 3 + Step(i);
        }
        data[index] = s;
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
        stream.Launch((Index1D)4, index => Kernels.AllocateInHelperInLoopKernel(index, buffer.View, 6));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
