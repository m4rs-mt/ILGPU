// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConstantRecursion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: ConstantRecursion
// Recursive helpers called with constant arguments are evaluated at compile time,
// so that no recursive call remains.
// Expected output: 1120 1121 1122 1123

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);

    static bool IsEven(int n) => n == 0 || IsOdd(n - 1);

    static bool IsOdd(int n) => n != 0 && IsEven(n - 1);

    public static void ConstantRecursionKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Factorial(5) + (IsOdd(7) ? 1000 : 0) + index;
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
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)4, index => Kernels.ConstantRecursionKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
