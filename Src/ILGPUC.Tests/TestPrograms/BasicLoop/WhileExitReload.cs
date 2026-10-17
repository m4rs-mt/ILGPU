// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WhileExitReload.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: WhileExitReload
// A loop loads an element and then a second element addressed by the first
// one before testing its exit condition; only the second load is used after
// the loop, so both loads have to be the ones of the last test.
// Expected output: data[data[n] + 50] * 1000 + 10 * sum of data[0..n) + lane
// per lane, where n = 2 + lane % 5.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WhileExitReloadKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int lane = index.X;
        int n = 2 + lane % 5;
        int i = 0, s = 0, q;
        while (true)
        {
            int p = data[i];
            q = data[p + 50];
            if (i >= n)
                break;
            s += p;
            i++;
        }
        output[index] = q * 1000 + s * 10 + lane;
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

        var values = new int[100];
        for (int i = 0; i < values.Length; i++)
            values[i] = (i * 7 + 3) % 41;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.WhileExitReloadKernel(index, output.View, data.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
