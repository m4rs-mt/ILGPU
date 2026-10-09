// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StaticLambdaAdd.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: StaticLambdaAdd
// Non-capturing lambda: add(index, 1) = index + 1.
// Expected output: 1 2 3 4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void StaticLambdaAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Func<int, int, int> add = (a, b) => a + b;
        data[index] = add(index.X, 1);
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
        stream.Launch(
            (Index1D)4,
            index => Kernels.StaticLambdaAddKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
