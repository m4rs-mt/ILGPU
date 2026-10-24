// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfDivision.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfDivision
// Half and float divisions by values and by constants and differences of
// quotients and products. With fusion turned off, the divisions are
// correctly rounded, and a / b - o * v and a * b - o * v round the quotient or
// product before the subtraction instead of fusing them into a multiply-add
// (which turns the NaN of inf - inf into an infinity). Each lane prints the
// bits of every Half and float result (-1 for NaN, whose bit pattern differs
// between the backends).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 8;

    internal static long Bits(Half value) =>
        Half.IsNaN(value) ? -1 : BitConverter.HalfToUInt16Bits(value);

    internal static long Bits(float value) =>
        float.IsNaN(value) ? -1 : BitConverter.SingleToUInt32Bits(value);

    public static void HalfDivisionKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> h,
        ArrayView1D<float, Stride1D.Dense> f,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        Half a = h[index * 4], b = h[index * 4 + 1];
        Half o = h[index * 4 + 2], v = h[index * 4 + 3];
        float x = f[index * 4], y = f[index * 4 + 1];
        float p = f[index * 4 + 2], q = f[index * 4 + 3];
        int offset = index * NumResults;
        output[offset] = Bits(a / b);
        output[offset + 1] = Bits(a / (Half)3.0f);
        output[offset + 2] = Bits(a / (Half)0.3f);
        output[offset + 3] = Bits(a / b - o * v);
        output[offset + 4] = Bits(a * b - o * v);
        output[offset + 5] = Bits(x / y - p * q);
        output[offset + 6] = Bits(x * y - p * q);
        output[offset + 7] = Bits(x / 3.0f);
    }
}

static class Program
{
    // a, b, o and v of every lane
    internal static readonly Half[] H =
    [
        (Half)1.0f, (Half)3.0f, (Half)0.1f, (Half)3.3f,
        (Half)2.0f, (Half)3.0f, (Half)1.1f, (Half)0.6f,
        (Half)1.0f, (Half)7.0f, (Half)0.3f, (Half)0.47f,
        (Half)5.0f, (Half)9.0f, (Half)0.7f, (Half)0.79f,
        (Half)0.1f, (Half)0.3f, (Half)1.9f, (Half)0.17f,
        (Half)(-7.0f), (Half)11.0f, (Half)2.3f, (Half)(-0.3f),
        (Half)4.25f, (Half)13.0f, (Half)7.7f, (Half)9.9f,
        (Half)3.14159f, (Half)1.7f, (Half)1.3f, (Half)1.9f,
        (Half)60000.0f, (Half)0.5f, (Half)300.0f, (Half)300.0f,
        (Half)60000.0f, (Half)0.5f, (Half)(-300.0f), (Half)300.0f,
        (Half)(-60000.0f), (Half)0.25f, (Half)300.0f, (Half)(-300.0f),
        (Half)1.1f, (Half)1.3f, (Half)1.1f, (Half)0.9f,
        (Half)0.3f, (Half)0.7f, (Half)0.9f, (Half)0.37f,
        (Half)65504.0f, (Half)2.0f, (Half)256.0f, (Half)300.0f,
        (Half)1.0f, (Half)6.0f, (Half)0.4f, (Half)0.41f,
        (Half)6.1e-5f, (Half)5.0f, (Half)0.003f, (Half)0.007f,
    ];

    // x, y, p and q of every lane
    internal static readonly float[] F =
    [
        1.0f, 3.0f, 0.1f, 3.3333333f,
        2.0f, 3.0f, 1.1f, 0.6060606f,
        1.0f, 7.0f, 0.3f, 0.47619048f,
        5.0f, 9.0f, 0.7f, 0.7936508f,
        0.1f, 0.3f, 1.9f, 0.17543860f,
        -7.0f, 11.0f, 2.3f, -0.27667984f,
        4.25f, 13.0f, 7.7f, 0.042457946f,
        3.14159f, 1.7f, 1.3f, 1.4215339f,
        3.0e38f, 0.5f, 2.0e19f, 3.0e19f,
        3.0e38f, 0.5f, -2.0e19f, 3.0e19f,
        -3.0e38f, 0.25f, 2.0e19f, -3.0e19f,
        1.1f, 1.3f, 1.1f, 1.3f,
        0.3f, 0.7f, 0.9f, 0.23333333f,
        3.4e38f, 2.0f, 1.8446744e19f, 1.8446744e19f,
        1.0f, 6.0f, 0.4f, 0.41666666f,
        1.0e-37f, 5.0f, 3.0e-19f, 6.6666664e-20f,
    ];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        int numLanes = H.Length / 4;
        using var h = stream.Allocate1D(H);
        using var f = stream.Allocate1D(F);
        using var output = stream.Allocate1D<long>(Kernels.NumResults * numLanes);
        stream.Launch((Index1D)numLanes, index => Kernels.HalfDivisionKernel(index, h.View, f.View, output.View));
        stream.Synchronize();

        foreach (var value in output.GetAsArray1D())
            Console.WriteLine(value);
    }
}
