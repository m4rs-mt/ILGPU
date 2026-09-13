// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicMaxJoin.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: AtomicMaxJoin
// A float Atomic.Max (a compare-and-swap loop on targets without a native float max)
// in a branch whose result flows into the join of an if/else.
// Expected output: 2 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicMaxJoinKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        float r;
        if (index == 0)
            r = Atomic.Max(ref data[0], 5.0f);
        else
            r = 1.0f;
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

        using var data = stream.Allocate1D(new float[] { 2.0f });
        using var output = stream.Allocate1D<float>(2);
        stream.Launch((Index1D)2, index => Kernels.AtomicMaxJoinKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
