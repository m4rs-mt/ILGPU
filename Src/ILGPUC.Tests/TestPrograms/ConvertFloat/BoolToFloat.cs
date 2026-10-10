// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BoolToFloat.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: BoolToFloat
// Kernel converts comparison results (bools at run time) to float: true is 1.0 and
// false is 0.0, also when the converted value is used in arithmetic.
// values = [-2, -1, 1, 2]. Expected output (two per lane): 0 1 0 0 1 3.5 1 3.5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void BoolToFloatKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> values,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        int v = values[index];
        float positive = (v > 0 ? 1 : 0);
        float notMinusOne = (v != -1 ? 1 : 0);
        output[index * 2] = positive;
        output[index * 2 + 1] = positive * 2.5f + notMinusOne;
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

        using var values = stream.Allocate1D(new[] { -2, -1, 1, 2 });
        using var output = stream.Allocate1D<float>(8);
        stream.Launch(
            (Index1D)4,
            index => Kernels.BoolToFloatKernel(index, values.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
