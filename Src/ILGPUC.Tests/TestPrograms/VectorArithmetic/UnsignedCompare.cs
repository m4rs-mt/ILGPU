// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnsignedCompare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: UnsignedCompare
// Unsigned comparisons of values that are negative as signed integers.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x)
    {
        uint a = (uint)(x - 4);
        ulong b = (ulong)(long)(x - 4);
        return (a > 2u ? 1 : 0) + (b < 3ul ? 2 : 0) + (a / 2u > 5u ? 4 : 0);
    }

    public static void UnsignedCompareKernel(
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
        stream.Launch((Index1D)12, index => Kernels.UnsignedCompareKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
