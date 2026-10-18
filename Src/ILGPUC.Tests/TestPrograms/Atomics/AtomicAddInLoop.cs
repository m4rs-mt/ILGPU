// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicAddInLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: AtomicAddInLoop
// Each thread atomically increments its own counter three times in a while
// loop and keeps the value returned by the last increment. The atomic result
// flows into the next iteration, so it must run exactly once per iteration.
// With 4 threads: counters start at 0, the last increment returns 2 and the
// counter ends at 3, so every thread writes 2 * 10 + 3.
// Expected output: 23 23 23 23

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicAddInLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> counters,
        ArrayView1D<int, Stride1D.Dense> data,
        int count)
    {
        int i = 0;
        int prev = -1;
        while (i < count)
        {
            prev = Atomic.Add(ref counters[index], 1);
            i++;
        }
        data[index] = prev * 10 + counters[index];
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

        using var counters = stream.Allocate1D<int>(4);
        counters.MemSetToZero();
        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.AtomicAddInLoopKernel(index, counters.View, buffer.View, 3));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
