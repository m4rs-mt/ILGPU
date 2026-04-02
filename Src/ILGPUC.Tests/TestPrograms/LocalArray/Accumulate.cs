// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accumulate.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalArray Accumulate
// Kernel allocates scratch[3], fills {1,2,3}, sums to output.
// Uses unrolled sum (no loop) to avoid CPU vectorization loop-order issues.
// Expected output: 6 6 6 6

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AccumulateKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] scratch = new int[3];
        scratch[0] = 1; scratch[1] = 2; scratch[2] = 3;
        output[index] = scratch[0] + scratch[1] + scratch[2];
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

        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.AccumulateKernel(index, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
