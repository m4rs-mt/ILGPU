// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecursiveStores.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: RecursiveStores
// A helper stores through a view at every depth of its recursion and returns
// early at the end, writing a different number of elements per thread.
// Expected output: 1 0 0 0 5 6 0 0 9 10 13 0 13 14 17 22

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static void Fill(ArrayView1D<int, Stride1D.Dense> data, int offset, int i, int n)
    {
        if (i >= n)
            return;
        data[offset + i] = i * i + offset + 1;
        Fill(data, offset, i + 1, n);
    }

    public static void RecursiveStoresKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Fill(data, index * 4, 0, index + 1);
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
            (Index1D)4, index => Kernels.RecursiveStoresKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
