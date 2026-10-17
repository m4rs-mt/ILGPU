// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UniformDivisorInactiveBranch.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: UniformDivisorInactiveBranch
// A division by a uniform zero divisor in a branch that no lane takes must
// not be evaluated.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x, int d)
    {
        if (x > 100)
            return x / d;
        return x + 1;
    }

    public static void UniformDivisorInactiveBranchKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output, int d)
    {
        output[index] = Compute(index.X % 9, d);
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
        stream.Launch((Index1D)12, index => Kernels.UniformDivisorInactiveBranchKernel(index, output.View, 0));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
