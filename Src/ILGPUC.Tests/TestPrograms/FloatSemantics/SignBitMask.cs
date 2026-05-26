// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SignBitMask.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SignBitMask
// The sign mask 0x80000000 is the minimum int value, whose literal must keep
// the 32-bit type (a plain -2147483648 literal is a 64-bit value in C).
// Expected output: 1 -1 -1 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SignBitMaskKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        uint sign = Interop.FloatAsInt(data[index]) & 0x8000_0000u;
        output[index] = Interop.IntAsFloat(sign | 0x3F80_0000u);
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

        using var data = stream.Allocate1D(new float[] { 2.0f, -3.0f, -0.0f, 5.0f });
        using var output = stream.Allocate1D<float>(4);
        stream.Launch((Index1D)4, index => Kernels.SignBitMaskKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
