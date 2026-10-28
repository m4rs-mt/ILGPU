// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfConversions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfConversions
// Conversions between Half and the other primitive types round to nearest
// even, and narrowing out-of-range values gives infinity. Each lane prints
// the bits of h = (Half)f, then h converted to float (as bits), int, long,
// short, uint, byte and sbyte (clamped to their ranges), and the bits of Half
// values converted from i, (long)i and (byte)i.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 11;

    // Keeps out-of-range values away from the integer conversions, whose
    // overflow behavior differs between the backends.
    internal static Half Clamp(Half value, float limit) =>
        XMath.Clamp(value, (Half)(-limit), (Half)limit);

    internal static long Result(float f, int i, int k)
    {
        var h = (Half)f;
        return k switch
        {
            0 => BitConverter.HalfToUInt16Bits(h),
            1 => BitConverter.SingleToInt32Bits((float)h),
            2 => (int)Clamp(h, 60000.0f),
            3 => (long)Clamp(h, 60000.0f),
            4 => (short)Clamp(h, 30000.0f),
            5 => (uint)Clamp(XMath.Abs(h), 60000.0f),
            6 => (byte)Clamp(XMath.Abs(h), 255.0f),
            7 => (sbyte)Clamp(h, 100.0f),
            8 => BitConverter.HalfToUInt16Bits((Half)i),
            9 => BitConverter.HalfToUInt16Bits((Half)(long)i),
            _ => BitConverter.HalfToUInt16Bits((byte)(i & 0xFF)),
        };
    }

    public static void HalfConversionsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> floats,
        ArrayView1D<int, Stride1D.Dense> ints,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        for (int k = 0; k < NumResults; ++k)
            output[index * NumResults + k] = Result(floats[index], ints[index], k);
    }
}

static class Program
{
    internal static readonly float[] Floats =
    [
        1.0f, -2.5f, 0.1f, 65504.0f, 70000.0f, -1e-7f, 2049.0f, 2051.0f, 3.14159f,
        -0.0f, 1e-5f, 127.75f,
    ];

    internal static readonly int[] Ints =
    [
        0, 1, -1, 2049, 2051, 65504, 70000, -300, 255, 1024, -2048, 12345,
    ];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var floats = stream.Allocate1D(Floats);
        using var ints = stream.Allocate1D(Ints);
        using var output = stream.Allocate1D<long>(Kernels.NumResults * Floats.Length);
        stream.Launch((Index1D)Floats.Length, index => Kernels.HalfConversionsKernel(index, floats.View, ints.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
