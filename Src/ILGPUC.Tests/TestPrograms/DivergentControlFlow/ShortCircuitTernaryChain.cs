// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ShortCircuitTernaryChain.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: ShortCircuitTernaryChain
// A conditional expression on three short-circuit conditions, the second of
// which divides by a value the first one checks for zero.
// Each lane uses x = index % 9; twelve lanes span two SIMD groups.
// Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ShortCircuitTernaryChainKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int x = index.X % 9;
        int a = x - 2;
        output[index] = a != 0 && 12 / a > 1 && (x & 1) == 0 ? x * 5 : x + 100;
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
        stream.Launch((Index1D)12, index => Kernels.ShortCircuitTernaryChainKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
