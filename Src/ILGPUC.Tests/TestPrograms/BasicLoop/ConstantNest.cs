// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConstantNest.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: ConstantNest
// Two nested loops with constant trip counts accumulate a value that depends
// on both induction variables and a per-lane factor x = index % 9.
// Expected output: one sum per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ConstantNestKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output)
    {
        int x = index.X % 9;
        long sum = 0;
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
                sum += (i * 8 + j) * x + (i ^ j);
        }
        output[index] = sum;
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
        stream.Launch((Index1D)12, index => Kernels.ConstantNestKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
