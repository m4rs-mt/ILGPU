// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfDoubleConversions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfDoubleConversions
// Conversions between Half and double round to nearest even. Each lane
// prints h = (Half)f converted to double (times 1000) and the bits of
// (Half)(double)f and (Half)((double)f * 3.0).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 3;

    internal static long Result(float f, int k)
    {
        var h = (Half)f;
        return k switch
        {
            0 => (long)((double)h * 1000.0),
            1 => BitConverter.HalfToUInt16Bits((Half)(double)f),
            _ => BitConverter.HalfToUInt16Bits((Half)((double)f * 3.0)),
        };
    }

    public static void HalfDoubleConversionsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> floats,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        for (int k = 0; k < NumResults; ++k)
            output[index * NumResults + k] = Result(floats[index], k);
    }
}

static class Program
{
    internal static readonly float[] Floats =
    [
        1.0f, -2.5f, 0.1f, 65504.0f, 70000.0f, -1e-7f, 2049.0f, 2051.0f, 3.14159f,
        -0.0f, 1e-5f, 127.75f,
    ];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var floats = stream.Allocate1D(Floats);
        using var output = stream.Allocate1D<long>(Kernels.NumResults * Floats.Length);
        stream.Launch((Index1D)Floats.Length, index => Kernels.HalfDoubleConversionsKernel(index, floats.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
