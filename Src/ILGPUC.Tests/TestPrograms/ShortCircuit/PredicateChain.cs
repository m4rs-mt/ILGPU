// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PredicateChain.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PredicateChain
// A chain of eight short-circuit predicates (alternating && and || helpers),
// each selecting one bit of the result, over x = 0..15.
// Expected output: 170 87 81 92 170 113 29 197 170 21 213 85 170 85 85 85 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static bool InRange(int x, int lo, int hi) =>
        x != lo && x != hi && (x & 3) != 0;

    static bool OutOfRange(int x, int lo, int hi) =>
        x == lo || x == hi || (x & 3) == 0;

    public static void PredicateChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        data[index] =
            (InRange(x, 0, 3) ? 1 : 0) |
            (OutOfRange(x, 1, 4) ? 2 : 0) |
            (InRange(x, 2, 5) ? 4 : 0) |
            (OutOfRange(x, 3, 6) ? 8 : 0) |
            (InRange(x, 4, 7) ? 16 : 0) |
            (OutOfRange(x, 5, 8) ? 32 : 0) |
            (InRange(x, 6, 9) ? 64 : 0) |
            (OutOfRange(x, 7, 10) ? 128 : 0);
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

        var input = new int[16];
        for (int i = 0; i < input.Length; ++i)
            input[i] = i;
        using var data = stream.Allocate1D(input);
        stream.Launch((Index1D)input.Length, index => Kernels.PredicateChainKernel(index, data.View));
        stream.Synchronize();

        foreach (var v in data.GetAsArray1D())
            Console.WriteLine(v);
    }
}
