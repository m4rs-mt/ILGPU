// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GotoCaseNoCopies.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: GotoCaseNoCopies
// Switch cases continue in other cases through goto case and goto default;
// the values live in memory, so the jumps carry no phi copies.
// Each lane uses x = index % 9; twelve lanes span two SIMD groups.
// Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GotoCaseNoCopiesKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int x = index.X % 9;
        output[index] = x;
        switch (x % 4)
        {
            case 0:
                output[index] += 10;
                goto case 1;
            case 1:
                output[index] *= 3;
                break;
            case 2:
                output[index] -= 4;
                goto default;
            default:
                output[index] += 1000;
                break;
        }
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
        stream.Launch((Index1D)12, index => Kernels.GotoCaseNoCopiesKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
