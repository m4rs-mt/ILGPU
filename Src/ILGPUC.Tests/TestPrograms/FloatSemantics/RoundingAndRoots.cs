// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RoundingAndRoots.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: RoundingAndRoots
// MathF.Round rounds halfway cases in [1, 2) to even, XMath.Cbrt keeps the
// sign of negative inputs, XMath.Exp2 folds to 2^x, and float.IsInteger and
// float.IsOddInteger are exact. Each lane prints the results times 1000.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 6;

    internal static long Scale(float value) =>
        (long)(value * 1000.0f + (value < 0.0f ? -0.5f : 0.5f));

    internal static float Result(float x, int k) => k switch
    {
        0 => MathF.Round(x),
        1 => MathF.Round(-x),
        2 => XMath.Cbrt(x * x * x),
        3 => XMath.Exp2(3.0f) + x,
        4 => float.IsInteger(x) ? 1.0f : 0.0f,
        _ => float.IsOddInteger(x) ? 1.0f : 0.0f,
    };

    public static void RoundingAndRootsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        for (int k = 0; k < NumResults; ++k)
            output[index * NumResults + k] = Scale(Result(data[index], k));
    }
}

static class Program
{
    internal static readonly float[] Data = [1.5f, -1.5f, 0.5f, 2.5f, -2.0f, 3.0f, 1.25f, -0.75f];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var data = stream.Allocate1D(Data);
        using var output = stream.Allocate1D<long>(Kernels.NumResults * Data.Length);
        stream.Launch((Index1D)Data.Length, index => Kernels.RoundingAndRootsKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
