// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TripleLoopGotoStore.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: TripleLoopGotoStore
// A goto out of three nested loops to a store and return; lanes leave at
// different depths and iterations, or finish all loops.
// Each lane uses x = index % 9; twelve lanes span two SIMD groups.
// Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void TripleLoopGotoStoreKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int x = index.X % 9;
        int r = 0;
        for (int a = 0; a < 3; a++)
        {
            for (int b = 0; b < 3; b++)
            {
                for (int c = 0; c < 3; c++)
                {
                    r += a * 9 + b * 3 + c;
                    if (r > x * 12 + 5)
                        goto done;
                }
                r ^= b;
            }
            r += 7;
        }
        r = -r;
    done:
        output[index] = r * 10 + x;
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

        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.TripleLoopGotoStoreKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
