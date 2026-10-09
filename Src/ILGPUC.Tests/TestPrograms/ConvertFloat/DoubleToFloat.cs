// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DoubleToFloat.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DoubleToFloat
// Kernel converts double to float.
// Expected output: 2 2 2 2

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DoubleToFloatKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> data, double value)
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
        stream.Launch((Index1D)4, index => Kernels.DoubleToFloatKernel(index, buffer.View, 2.0));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine((int)v);
    }
}
