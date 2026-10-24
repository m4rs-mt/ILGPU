// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicMaxReadBack.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: AtomicMaxReadBack
// Reads the target of a float Atomic.Max (a compare-and-swap loop on targets without a
// native float max) after the atomic and passes the value read to a second Atomic.Max
// in a branch, which receives the value the first atomic stored.
// Expected output: 5 5 5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicMaxReadBackKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        Atomic.Max(ref data[0], 5.0f);
        float read = data[0];
        if (index == 0)
            Atomic.Max(ref data[1], read);
        output[index] = read;
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

        using var data = stream.Allocate1D(new float[] { 2.0f, 1.0f });
        using var output = stream.Allocate1D<float>(1);
        stream.Launch((Index1D)1, index => Kernels.AtomicMaxReadBackKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in data.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
