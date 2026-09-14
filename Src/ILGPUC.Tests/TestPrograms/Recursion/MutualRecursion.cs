// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MutualRecursion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: MutualRecursion
// Two pairs of helpers call each other: IsEven and IsOdd in tail position, Up
// and Down with work left after each call.
// Expected output: 1 0 0 2 1 6 0 14 1 30 0 62

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static bool IsEven(int n) => n == 0 || IsOdd(n - 1);

    static bool IsOdd(int n) => n != 0 && IsEven(n - 1);

    static int Up(int n) => Down(n - 1) + 1;

    static int Down(int n) => n <= 0 ? 0 : Up(n) * 2;

    public static void MutualRecursionKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index * 2] = IsEven(index) ? 1 : 0;
        data[index * 2 + 1] = Down(index);
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

        using var buffer = stream.Allocate1D<int>(12);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)6, index => Kernels.MutualRecursionKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
