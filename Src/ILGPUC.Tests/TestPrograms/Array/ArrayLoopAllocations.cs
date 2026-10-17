// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayLoopAllocations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: ArrayLoopAllocations
// Every iteration of a loop reuses the buffer of an array allocated in it, which
// gives the same results as in .NET as long as an iteration uses no array of an
// earlier iteration after allocating its own: arrays used in their iteration,
// the array of the last iteration used after the loop, the array of the previous
// iteration used before the allocation, arrays allocated before a loop and
// swapped in it, the arrays of a helper that is not inlined and Array.Empty.
// Every thread writes seven results.
// Expected output (per thread):
//   0: 1 201 3 3 1 1 1
//   1: 21 402 6 4 103 3 2
//   2: 24 603 10 5 306 6 3
//   3: 64 804 15 6 610 10 4

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int[] MakeArray(int value)
    {
        var values = new int[2];
        values[0] = value;
        return values;
    }

    public static void ArrayLoopAllocationKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int i = index.X;
        int n = i + 2;

        int perIteration = 0;
        for (int k = 0; k < n; ++k)
        {
            var a = new int[2];
            a[k & 1] += k;
            perIteration += a[0] * 10 + a[1];
        }

        int[] last = null;
        for (int k = 0; k < n; ++k)
        {
            var a = new int[2];
            a[0] = k;
            a[1] = 2 * k;
            last = a;
        }

        int previous = 0;
        int[] prev = null;
        for (int k = 0; k < i + 3; ++k)
        {
            if (prev != null)
                previous += prev[0];
            var a = new int[1];
            a[0] = k + 1;
            prev = a;
        }

        var x = new int[1];
        var y = new int[1];
        for (int k = 0; k < n; ++k)
        {
            x[0] = y[0] + k;
            (x, y) = (y, x);
        }

        int helper = 0;
        for (int k = 0; k < n; ++k)
        {
            var a = MakeArray(k);
            helper += a[0] + a[1];
            a[1] = 5;
        }

        int same = 0;
        int[] empty = null;
        for (int k = 0; k < n; ++k)
        {
            var e = Array.Empty<int>();
            same += e == empty ? 1 : 0;
            empty = e;
        }

        int o = i * 7;
        output[o + 0] = perIteration;
        output[o + 1] = last[0] + last[1] * 100;
        output[o + 2] = previous;
        output[o + 3] = prev[0];
        output[o + 4] = x[0] * 100 + y[0];
        output[o + 5] = helper;
        output[o + 6] = same;
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

        using var output = stream.Allocate1D<int>(28);
        stream.Launch(
            (Index1D)4,
            index => Kernels.ArrayLoopAllocationKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
