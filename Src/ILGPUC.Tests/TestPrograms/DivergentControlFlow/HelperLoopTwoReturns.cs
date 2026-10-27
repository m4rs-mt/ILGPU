// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HelperLoopTwoReturns.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HelperLoopTwoReturns
// A helper returns from two places inside a loop, or after the loop.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Search(int x)
    {
        int acc = 1;
        for (int k = 0; k < 12; k++)
        {
            acc = acc * 3 % 17;
            if (acc == x)
                return k * 100 + acc;
            if (acc + k > 15 + x * 3)
                return -k;
        }
        return acc + 1000;
    }

    public static void HelperLoopTwoReturnsKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = Search(index.X % 9);
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
        stream.Launch((Index1D)12, index => Kernels.HelperLoopTwoReturnsKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
