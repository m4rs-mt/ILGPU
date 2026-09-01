// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ThreadVaryingScratch.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalArray ThreadVaryingScratch
// Every existing LocalArray program fills its scratch buffer with values that are
// the same in every thread, so a backend that shares one buffer across threads
// still produces the right answer. This one writes thread-dependent values and
// reads back through a thread-dependent index, so the buffer must be private per
// thread. Thread i writes {i*10, i*10+1, i*10+2, i*10+3} and reads slot i & 3.
// Expected output: 0 11 22 33

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ThreadVaryingScratchKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] scratch = new int[4];
        scratch[0] = index.X * 10;
        scratch[1] = index.X * 10 + 1;
        scratch[2] = index.X * 10 + 2;
        scratch[3] = index.X * 10 + 3;
        output[index] = scratch[index.X & 3];
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

        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.ThreadVaryingScratchKernel(index, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
