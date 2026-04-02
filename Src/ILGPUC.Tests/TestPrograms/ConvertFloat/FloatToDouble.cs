// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloatToDouble.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: FloatToDouble
// Kernel converts float to double.
// Expected output: 3 3 3 3

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void FloatToDoubleKernel(
        Index1D index, ArrayView1D<double, Stride1D.Dense> data, float value)
    {
        data[index] = (double)value;
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

        using var buffer = stream.Allocate1D<double>(4);
        stream.Launch((Index1D)4, index => Kernels.FloatToDoubleKernel(index, buffer.View, 3.0f));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine((int)v);
    }
}
