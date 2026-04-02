// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Length.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalArray Length
// Kernel allocates local int[5], queries .Length, writes to output.
// Expected output: 5 5 5 5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LengthKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] local = new int[5];
        output[index] = local.Length;
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
        stream.Launch((Index1D)4, index => Kernels.LengthKernel(index, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
