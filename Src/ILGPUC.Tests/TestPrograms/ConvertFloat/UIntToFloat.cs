// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UIntToFloat.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: UIntToFloat
// Kernel converts an unsigned int above int.MaxValue to float (conv.r.un).
// Expected output: 4000000000 4000000000 4000000000 4000000000

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void UIntToFloatKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> data, uint value)
    {
        data[index] = (float)value;
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

        using var buffer = stream.Allocate1D<float>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.UIntToFloatKernel(index, buffer.View, 4000000001u));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine((long)v);
    }
}
