// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecursionWithLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: RecursionWithLoop
// A helper runs a loop before calling itself, and calls a small helper that is
// still inlined into it.
// Expected output: 0 0 2 8 20 40 70 112

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Twice(int x) => 2 * x;

    static int SumOfSums(int n)
    {
        if (n <= 0)
            return 0;
        int sum = 0;
        for (int i = 0; i < n; ++i)
            sum += i;
        return Twice(sum) + SumOfSums(n - 1);
    }

    public static void RecursionWithLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = SumOfSums(index);
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

        using var buffer = stream.Allocate1D<int>(8);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)8, index => Kernels.RecursionWithLoopKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
