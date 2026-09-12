// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoopHelperCalledTwice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: LoopHelperCalledTwice
// A helper whose loop carries the result of an atomic, inlined twice into one kernel.
// Single thread.
// Expected output: 2 5 6 (first result, second result, final counter)

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Spin(ArrayView1D<int, Stride1D.Dense> counter, int n)
    {
        int last = -1;
        for (int k = 0; k < n; k++)
            last = Atomic.Add(ref counter[0], 1);
        return last;
    }

    public static void LoopHelperCalledTwiceKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> counter,
        ArrayView1D<int, Stride1D.Dense> output,
        int n)
    {
        int r1 = Spin(counter, n);
        int r2 = Spin(counter, n);
        output[0] = r1;
        output[1] = r2;
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

        using var counter = stream.Allocate1D<int>(1);
        counter.MemSetToZero(stream);
        using var output = stream.Allocate1D<int>(2);
        stream.Launch((Index1D)1, index => Kernels.LoopHelperCalledTwiceKernel(index, counter.View, output.View, 3));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
        Console.WriteLine(counter.GetAsArray1D()[0]);
    }
}
