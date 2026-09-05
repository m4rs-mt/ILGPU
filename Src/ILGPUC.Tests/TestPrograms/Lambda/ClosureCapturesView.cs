// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ClosureCapturesView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ClosureCapturesView
// A lambda captures a view of global memory and reads it in reverse order.
// Closures may capture views of global memory, unlike views of shared or
// local memory.
// Expected output: 30 20 10 0 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ClosureCapturesViewKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, ArrayView1D<int, Stride1D.Dense> source)
    {
        Func<int, int> read = j => source[3 - j] * 10;
        data[index] = read(index.X);
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

        using var source = stream.Allocate1D<int>(4);
        source.View.CopyFromCPU(stream, new[] { 0, 1, 2, 3 });
        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.ClosureCapturesViewKernel(index, buffer.View, source.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
