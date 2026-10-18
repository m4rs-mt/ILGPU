// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MixedNesting1.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: MixedNesting1
// Truth-table sweep of (a || b) && c over the thread index bits.
// Expected output: 0 0 0 0 0 1 1 1 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void MixedNesting1Kernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        if (((index.X & 1) != 0 || (index.X & 2) != 0) && (index.X & 4) != 0)
            data[index] = 1;
        else
            data[index] = 0;
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
        stream.Launch((Index1D)8, index => Kernels.MixedNesting1Kernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
