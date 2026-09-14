// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericFactorial.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GenericFactorial
// A generic helper calls itself, and each of its two instantiations is a recursive
// function of its own: one multiplies, the other one adds.
// Expected output: 2 5 12 34 135 741 5068 40356

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Combine<T>(int n, int value)
        where T : struct =>
        typeof(T) == typeof(int) ? n * value : n + value;

    static int Fold<T>(int n)
        where T : struct =>
        n <= 1 ? 1 : Combine<T>(n, Fold<T>(n - 1));

    public static void GenericFactorialKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Fold<int>(index + 1) + Fold<float>(index + 1);
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
            (Index1D)8, index => Kernels.GenericFactorialKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
