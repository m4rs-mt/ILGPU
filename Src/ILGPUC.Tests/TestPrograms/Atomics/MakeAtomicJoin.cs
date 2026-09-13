// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MakeAtomicJoin.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: MakeAtomicJoin
// A custom atomic built with Atomic.MakeAtomic (a compare-and-swap loop on every
// target) in a branch whose result flows into the join of an if/else.
// Expected output: 2 1 5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void MakeAtomicJoinKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int r;
        if (index == 0)
        {
            r = Atomic.MakeAtomic(
                ref data[0],
                5,
                (current, value) => current > value ? current : value,
                (ref int target, int compare, int value) =>
                    Atomic.CompareExchange(ref target, compare, value));
        }
        else
        {
            r = 1;
        }
        output[index] = r;
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

        using var data = stream.Allocate1D(new int[] { 2 });
        using var output = stream.Allocate1D<int>(2);
        stream.Launch((Index1D)2, index => Kernels.MakeAtomicJoinKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
        Console.WriteLine(data.GetAsArray1D()[0]);
    }
}
