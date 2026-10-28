// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Fibonacci.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: Fibonacci
// A helper calls itself twice and adds the results, so the result of the first
// call has to survive the second one.
// Expected output: 0 1 1 2 3 5 8 13 21 34

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Fibonacci(int n) => n < 2 ? n : Fibonacci(n - 1) + Fibonacci(n - 2);

    public static void FibonacciKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Fibonacci(index);
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

        using var buffer = stream.Allocate1D<int>(10);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)10, index => Kernels.FibonacciKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
