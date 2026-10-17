// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeepChain.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DeepChain
// Ten-term and-chain; thread i clears bit i (i < 10), thread 10 keeps all bits set.
// Expected output: 0 0 0 0 0 0 0 0 0 0 1 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DeepChainKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int v = index.X < 10 ? 0x3FF ^ (1 << index.X) : 0x3FF;
        if ((v & 1) != 0 && (v & 2) != 0 && (v & 4) != 0 && (v & 8) != 0 &&
            (v & 16) != 0 && (v & 32) != 0 && (v & 64) != 0 && (v & 128) != 0 &&
            (v & 256) != 0 && (v & 512) != 0)
        {
            data[index] = 1;
        }
        else
        {
            data[index] = 0;
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

        using var buffer = stream.Allocate1D<int>(11);
        stream.Launch((Index1D)11, index => Kernels.DeepChainKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
