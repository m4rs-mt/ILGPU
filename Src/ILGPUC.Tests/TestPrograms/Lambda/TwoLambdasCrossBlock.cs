// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TwoLambdasCrossBlock.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: TwoLambdasCrossBlock
// Two different non-capturing lambdas whose delegate handles cross a block boundary on
// the evaluation stack, because each argument is a ternary. Each call site must
// devirtualize to its own lambda body; resolving both to the first lambda computes
// (x + 1) + (x + 1) instead of (x + 1) + (x * 2).
//
// index.X == i -> (i + 1) + (i * 2)
// Expected output: 1 4 7 10 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void TwoLambdasCrossBlockKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int flag)
    {
        Func<int, int> inc = x => x + 1;
        Func<int, int> dbl = x => x * 2;
        data[index] =
            inc(flag > 0 ? index.X : 0) +
            dbl(flag > 0 ? index.X : 1);
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.TwoLambdasCrossBlockKernel(index, buffer.View, 1));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
