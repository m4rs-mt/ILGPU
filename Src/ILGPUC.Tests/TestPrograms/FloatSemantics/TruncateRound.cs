// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TruncateRound.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: TruncateRound
// MathF.Truncate, MathF.Round (to even) and MathF.Round away from zero.
// Inputs: 2.7, -2.7, 2.5, -3.5 -> truncate, round, round away per input.
// Expected output: 2 3 3 -2 -3 -3 2 2 3 -3 -4 -4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void TruncateRoundKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        float x = data[index];
        output[index * 3 + 0] = MathF.Truncate(x);
        output[index * 3 + 1] = MathF.Round(x);
        output[index * 3 + 2] = MathF.Round(x, MidpointRounding.AwayFromZero);
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

        using var data = stream.Allocate1D(new float[] { 2.7f, -2.7f, 2.5f, -3.5f });
        using var output = stream.Allocate1D<float>(12);
        stream.Launch((Index1D)4, index => Kernels.TruncateRoundKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
