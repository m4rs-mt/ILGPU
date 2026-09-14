// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Factorial.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: Factorial
// A helper calls itself, and the depth of the recursion differs per thread.
// Expected output: 1 2 6 24 120 720 5040 40320

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);

    public static void FactorialKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Factorial(index + 1);
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
            (Index1D)8, index => Kernels.FactorialKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
