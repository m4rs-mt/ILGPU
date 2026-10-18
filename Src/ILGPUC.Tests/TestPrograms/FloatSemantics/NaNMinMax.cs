// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NaNMinMax.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NaNMinMax
// Math.Min/Max, MathF.Min/Max and Math.Clamp propagate NaN like .NET does,
// unlike C's fmin/fmax which return the other operand.
// Inputs: x = 1, NaN -> each line is 1 if the result is NaN, else 0.
// Expected output: 0 0 0 0 0 1 1 1 1 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NaNMinMaxKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        float x = data[index];
        float y = 2.0f;
        output[index * 5 + 0] = float.IsNaN(Math.Max(x, y)) ? 1 : 0;
        output[index * 5 + 1] = float.IsNaN(Math.Min(y, x)) ? 1 : 0;
        output[index * 5 + 2] = float.IsNaN(MathF.Max(y, x)) ? 1 : 0;
        output[index * 5 + 3] = float.IsNaN(MathF.Min(x, y)) ? 1 : 0;
        output[index * 5 + 4] = float.IsNaN(Math.Clamp(x, 0.0f, y)) ? 1 : 0;
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

        using var data = stream.Allocate1D(new float[] { 1.0f, float.NaN });
        using var output = stream.Allocate1D<int>(10);
        stream.Launch((Index1D)2, index => Kernels.NaNMinMaxKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
