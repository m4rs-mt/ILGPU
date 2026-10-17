// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NaNInfinityChecks.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NaNInfinityChecks
// float.IsNaN, float.IsInfinity and the x != x NaN idiom must survive native
// compilation (fast math would fold them away).
// Inputs: 1, NaN, +Inf, -Inf -> bit 0 IsNaN, bit 1 IsInfinity, bit 2 x != x.
// Expected output: 0 5 2 2

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NaNInfinityChecksKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> data, ArrayView1D<int, Stride1D.Dense> output)
    {
        float x = data[index];
        output[index] =
            (float.IsNaN(x) ? 1 : 0) +
            (float.IsInfinity(x) ? 2 : 0) +
            (x != x ? 4 : 0);
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

        using var data = stream.Allocate1D<float>(4);
        data.View.CopyFromCPU(stream, new float[] { 1.0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity });
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.NaNInfinityChecksKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
