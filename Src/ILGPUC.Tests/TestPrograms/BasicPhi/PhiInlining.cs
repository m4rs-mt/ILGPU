// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PhiInlining.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PhiInlining
// Kernel merges values from inlined branches (phi node at join point).
// a=10, b=3: result = a - b = 7
// Expected output: 7 7 7 7

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PhiInliningKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b)
    {
        int x;
        if (a > b)
            x = a - b;
        else
            x = b - a;

        data[index] = x;
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
        stream.Launch((Index1D)4, index => Kernels.PhiInliningKernel(index, buffer.View, 10, 3));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
