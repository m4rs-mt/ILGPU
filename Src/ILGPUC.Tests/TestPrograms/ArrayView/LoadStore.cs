// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoadStore.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: LoadStore
// Kernel copies source to target.
// Expected output: 10 20 30 40

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LoadStoreKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> source,
        ArrayView1D<int, Stride1D.Dense> target)
    {
        target[index] = source[index];
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

        int[] input = [10, 20, 30, 40];
        using var source = stream.Allocate1D(input);
        using var target = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.LoadStoreKernel(index, source.View, target.View));
        stream.Synchronize();

        var data = target.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
