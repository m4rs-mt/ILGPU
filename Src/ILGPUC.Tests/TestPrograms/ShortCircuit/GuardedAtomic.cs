// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GuardedAtomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: GuardedAtomic
// The right operand of && performs an atomic increment. Only the threads the
// left operand admits (index > 1) may execute it.
// Expected output: 0 0 1 1 2 (flags per thread, then the counter)

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GuardedAtomicKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> flags, ArrayView1D<int, Stride1D.Dense> counter)
    {
        flags[index] = index.X > 1 && Atomic.Add(ref counter[0], 1) >= 0 ? 1 : 0;
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

        using var flags = stream.Allocate1D<int>(4);
        using var counter = stream.Allocate1D<int>(1);
        counter.View.MemSetToZero(stream);
        stream.Launch((Index1D)4, index => Kernels.GuardedAtomicKernel(index, flags.View, counter.View));
        stream.Synchronize();

        foreach (var v in flags.GetAsArray1D())
            Console.WriteLine(v);
        Console.WriteLine(counter.GetAsArray1D()[0]);
    }
}
