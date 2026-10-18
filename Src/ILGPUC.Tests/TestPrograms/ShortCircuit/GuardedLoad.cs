// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GuardedLoad.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: GuardedLoad
// The right operand of && loads at a huge offset that is only in bounds for the
// thread the left operand admits. Speculating the load faults (or reads garbage).
// Expected output: 7 0 0 0

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GuardedLoadKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, ArrayView1D<int, Stride1D.Dense> output, int limit)
    {
        long offset = (long)index.X * 100000000L;
        output[index] = index.X < limit && data[offset] > 0 ? 7 : 0;
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

        using var data = stream.Allocate1D<int>(4);
        data.View.CopyFromCPU(stream, new int[] { 1, 1, 1, 1 });
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.GuardedLoadKernel(index, data.View, output.View, 1));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
