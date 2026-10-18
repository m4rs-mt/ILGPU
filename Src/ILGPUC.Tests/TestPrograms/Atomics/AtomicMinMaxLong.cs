// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicMinMaxLong.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: AtomicMinMaxLong
// 64-bit signed Atomic.Min / Atomic.Max with negative operands. Threads 0..3
// contribute (index - 2) * 1000, i.e. -2000, -1000, 0 and 1000; an unsigned
// comparison would pick 0 as the minimum and -1000 as the maximum.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicMinMaxLongKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        long value = (index - 2) * 1000L;
        Atomic.Min(ref data[0], value);
        Atomic.Max(ref data[1], value);
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

        long[] init = [0L, -1_000_000L];
        using var buffer = stream.Allocate1D(init);
        stream.Launch(
            (Index1D)4,
            index => Kernels.AtomicMinMaxLongKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        Console.WriteLine(data[0]);
        Console.WriteLine(data[1]);
    }
}
