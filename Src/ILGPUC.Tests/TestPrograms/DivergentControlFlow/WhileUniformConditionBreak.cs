// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WhileUniformConditionBreak.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: WhileUniformConditionBreak
// A loop with a uniform condition is left early through a per-lane break.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x, int u)
    {
        int k = 0, s = 0;
        while (k < u)
        {
            if (k == x)
                break;
            s += k * 2;
            k++;
        }
        return s * 100 + k;
    }

    public static void WhileUniformConditionBreakKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output, int u)
    {
        output[index] = Compute(index.X % 9, u);
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
        stream.Launch((Index1D)12, index => Kernels.WhileUniformConditionBreakKernel(index, output.View, 10));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
