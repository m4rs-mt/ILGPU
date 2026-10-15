// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloatIdentities.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: FloatIdentities
// Float operations with a constant zero or one keep their IEEE 754 results:
// x + 0.0 is +0.0 for x = -0.0, x * 0.0 is NaN for infinities, x - x and
// x / x are NaN for infinities and NaN, nested constants are not
// reassociated, and constant IEEE remainders round the quotient to even.
// Each lane prints the result bits (-1 for NaN, whose bits differ between
// the backends).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 14;

    internal static float Result(float x, int k) => k switch
    {
        0 => x + 0.0f,
        1 => 0.0f + x,
        2 => x + -0.0f,
        3 => x - 0.0f,
        4 => x - -0.0f,
        5 => 0.0f - x,
        6 => -0.0f - x,
        7 => x * 0.0f,
        8 => 0.0f * x,
        9 => x - x,
        10 => x / x,
        11 => (x + 1e8f) - 1e8f,
        12 => (x * 3.0f) * 0.1f,
        _ => MathF.IEEERemainder(5.0f, 3.0f) + x,
    };

    internal static int Bits(float value) =>
        float.IsNaN(value) ? -1 : BitConverter.SingleToInt32Bits(value);

    public static void FloatIdentitiesKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        for (int k = 0; k < NumResults; ++k)
            output[index * NumResults + k] = Bits(Result(data[index], k));
    }
}

static class Program
{
    internal static readonly float[] Data =
        [-0.0f, 0.0f, 1.5f, -2.0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 3.0f];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var data = stream.Allocate1D(Data);
        using var output = stream.Allocate1D<int>(Kernels.NumResults * Data.Length);
        stream.Launch((Index1D)Data.Length, index => Kernels.FloatIdentitiesKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
