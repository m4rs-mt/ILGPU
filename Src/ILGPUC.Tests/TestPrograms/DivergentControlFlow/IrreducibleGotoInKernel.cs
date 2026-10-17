// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IrreducibleGotoInKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: IrreducibleGotoInKernel
// An irreducible loop written with gotos directly in the kernel body: every
// third lane enters the loop at its second label, all others at its first,
// and lanes leave it either from the middle or at the end of an iteration.
// Twelve lanes span two SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IrreducibleGotoInKernelKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int x = index.X + 1;
        int y = 0;
        if (index.X % 3 == 0)
            goto second;
    first:
        x = x * 3 + 1;
        y++;
        if (x % 7 == 0)
            goto done;
    second:
        x = x / 2 + y;
        y += 2;
        if (y < 12)
            goto first;
    done:
        output[index] = x * 100 + y;
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

        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.IrreducibleGotoInKernelKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
