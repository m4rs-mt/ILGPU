// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ThreeWayChain.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ThreeWayChain
// Truth-table sweep of if (a || b) 10 elif (c) 20 else 30 over the thread index bits.
// Expected output: 30 10 10 10 20 10 10 10 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ThreeWayChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        if ((index.X & 1) != 0 || (index.X & 2) != 0)
            data[index] = 10;
        else if ((index.X & 4) != 0)
            data[index] = 20;
        else
            data[index] = 30;
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
        stream.Launch((Index1D)8, index => Kernels.ThreeWayChainKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
