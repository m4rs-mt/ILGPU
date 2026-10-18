// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IrreducibleLoopEntryValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: IrreducibleLoopEntryValues
// A loop with two entry blocks that receive different initial values: lanes
// entering at the first block start from other values than lanes entering at
// the second one, including a value that stays constant inside the loop.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x)
    {
        long s;
        int k;
        int m;
        if ((x & 1) == 0)
        {
            s = 100;
            k = 3;
            m = 7;
            goto B;
        }
        s = -5;
        k = 0;
        m = 11;
    A:
        s += m * k;
        k++;
    B:
        s += k + m;
        k += 2;
        if (k < x + 4)
        {
            if ((k % 3) == 0)
                goto A;
            goto B;
        }
        return s * 10 + m;
    }

    public static void IrreducibleLoopEntryValuesKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output)
    {
        output[index] = Compute(index.X % 9);
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

        using var output = stream.Allocate1D<long>(12);
        stream.Launch((Index1D)12, index => Kernels.IrreducibleLoopEntryValuesKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
