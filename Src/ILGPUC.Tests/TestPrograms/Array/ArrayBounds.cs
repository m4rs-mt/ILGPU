// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayBounds.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ArrayBounds
// Kernel writes index if within bounds, else -1.
// Expected output: 0 1 -1 -1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ArrayBoundsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int length)
    {
        if (index.X < length)
            data[index] = index.X;
        else
            data[index] = -1;
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
        stream.Launch((Index1D)4, index => Kernels.ArrayBoundsKernel(index, buffer.View, 2));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
