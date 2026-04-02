// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NegLong.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NegLong
// Kernel negates longs.
// Input: [1L,2L,3L,4L], Expected output: -1 -2 -3 -4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NegKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> input,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        result[index] = -input[index];
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

        var inputData = new long[] { 1L, 2L, 3L, 4L };
        using var inputBuf = stream.Allocate1D(inputData);
        using var resultBuf = stream.Allocate1D<long>(4);

        stream.Launch((Index1D)4, index => Kernels.NegKernel(index, inputBuf.View, resultBuf.View));
        stream.Synchronize();

        var data = resultBuf.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
