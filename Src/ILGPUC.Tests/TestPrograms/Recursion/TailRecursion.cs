// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TailRecursion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: TailRecursion
// Helpers call themselves in tail position, which compiles to loops: Gcd returns
// the result of the call, SumTo returns it through a local, and Fill returns
// nothing. The number of iterations differs per thread.
// Expected output: 4 0 0 0 2 1 1 0 4 3 1 2 2 6 0 0

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    static int SumTo(int n, int sum)
    {
        if (n <= 0)
            return sum;
        int next = sum + n;
        return SumTo(n - 1, next);
    }

    static void Fill(ArrayView1D<int, Stride1D.Dense> data, int offset, int i, int n)
    {
        if (i >= n)
            return;
        data[offset + i] = i + 1;
        Fill(data, offset, i + 1, n);
    }

    public static void TailRecursionKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index * 4] = Gcd(index * 6, 4);
        data[index * 4 + 1] = SumTo(index, 0);
        Fill(data, index * 4 + 2, 0, index % 3);
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

        using var buffer = stream.Allocate1D<int>(16);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)4, index => Kernels.TailRecursionKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
