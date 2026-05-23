// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DiamondFeedChain.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DiamondFeedChain
// Truth-table sweep of t = a ? b : c; if (t && d) over the thread index bits.
// Expected output: 0 0 0 0 0 0 0 0 0 0 0 1 1 0 1 1 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DiamondFeedChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        bool t = (index.X & 1) != 0 ? (index.X & 2) != 0 : (index.X & 4) != 0;
        if (t && (index.X & 8) != 0)
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

        using var buffer = stream.Allocate1D<int>(16);
        stream.Launch((Index1D)16, index => Kernels.DiamondFeedChainKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
