// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WhileExitRecompute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: WhileExitRecompute
// A loop computes an index and loads through it before testing its exit
// condition; only the load is used after the loop, so the index has to be
// the one of the last test, not the one of the previous iteration.
// Expected output: data[3 * n + lane] * 1000 + sum of the earlier indices
// per lane, where n = 2 + lane % 5.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WhileExitRecomputeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int lane = index.X;
        int n = 2 + lane % 5;
        int i = 0, s = 0, q;
        while (true)
        {
            int p = i * 3 + lane;
            q = data[p];
            if (i >= n)
                break;
            s += p;
            i++;
        }
        output[index] = q * 1000 + s;
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

        var values = new int[64];
        for (int i = 0; i < values.Length; i++)
            values[i] = i * 7 % 31;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.WhileExitRecomputeKernel(index, output.View, data.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
