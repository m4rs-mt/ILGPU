// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedLoopGotoTargets.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: NestedLoopGotoTargets
// Three nested loops left per lane in three ways: a goto out of all loops, a
// goto to the end of the outer loop body and a break out of the inner loop.
// Each lane reads the input at a different offset, so lanes leave the loops
// at different depths and iterations. Twelve lanes span two SIMD groups.
// Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NestedLoopGotoTargetsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int a, b = 0, c = 0, r = 0;
        for (a = 0; a < 4; a++)
        {
            for (b = 0; b < 4; b++)
            {
                for (c = 0; c < 4; c++)
                {
                    int v = input[(a * 16 + b * 4 + c + index.X * 5) % 64];
                    if (v == 13)
                        goto done;
                    if (v > 40)
                        goto nextA;
                    if (v < -40)
                        break;
                    r += v;
                }
                r ^= b;
            }
        nextA:
            r += 1000;
        }
    done:
        output[index] = r * 7 + a * 100 + b * 10 + c;
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
            values[i] = (i * 37 + 11) % 101 - 50;

        using var input = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.NestedLoopGotoTargetsKernel(index, input.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
