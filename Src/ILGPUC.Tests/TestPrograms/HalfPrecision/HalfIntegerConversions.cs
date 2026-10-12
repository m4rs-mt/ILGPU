// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfIntegerConversions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfIntegerConversions
// Conversions between Half and char, the checked conversions, and the generic
// conversions (CreateChecked, CreateSaturating, CreateTruncating,
// ConvertToInteger and the conversions of the integer types from Half) match
// System.Half on the host. The generic conversions to integers saturate, so
// NaN converts to 0 and infinities to the bounds of the integer type. Checked
// conversions are applied to values in range. Each lane prints the integer
// results and the bits of the Half results (-1 for NaN).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 24;

    internal static long Bits(Half value) =>
        Half.IsNaN(value) ? -1 : BitConverter.HalfToUInt16Bits(value);

    internal static long Result(Half h, int i, int k)
    {
        var c = Half.IsFinite(h) ? h : Half.Zero;
        var a = Half.Abs(c);
        return k switch
        {
            0 => checked((int)c),
            1 => checked((char)a),
            2 => checked((short)(c / (Half)2.0f)),
            3 => checked((ushort)a),
            4 => (char)a,
            5 => checked((long)c),
            6 => (long)checked((ulong)a),
            7 => Bits((Half)(char)(i & 0xFFFF)),
            8 => checked((sbyte)(c / (Half)1000.0f)),
            9 => checked((byte)(a / (Half)1000.0f)),
            10 => Bits(Half.CreateChecked((ushort)(i & 0xFFFF))),
            11 => Bits(Half.CreateSaturating((ulong)(uint)i)),
            12 => Bits(Half.CreateTruncating((short)i)),
            13 => Half.ConvertToInteger<int>(h),
            14 => Half.ConvertToIntegerNative<short>(h),
            15 => Half.ConvertToInteger<byte>(h),
            16 => (long)Half.ConvertToInteger<ulong>(h),
            17 => int.CreateSaturating(h),
            18 => Bits(Half.CreateSaturating(i)),
            19 => Bits(Half.CreateTruncating((long)i * 3)),
            20 => Bits(Half.CreateChecked((char)(i & 0xFFFF))),
            21 => Bits(Half.CreateChecked((uint)i)),
            22 => Half.ConvertToInteger<char>(h),
            _ => checked((uint)a),
        };
    }

    public static void HalfIntegerConversionsKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> halfs,
        ArrayView1D<int, Stride1D.Dense> ints,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        for (int k = 0; k < NumResults; ++k)
            output[index * NumResults + k] = Result(halfs[index], ints[index], k);
    }
}

static class Program
{
    internal static readonly Half[] X =
    [
        (Half)1.5f, (Half)(-2.5f), Half.MaxValue, Half.MinValue, Half.NaN,
        Half.PositiveInfinity, Half.NegativeInfinity, Half.NegativeZero, (Half)1000.75f,
        (Half)0.1f, (Half)300.0f, (Half)(-300.0f),
    ];

    internal static readonly int[] Y =
    [
        0, 1, -1, 65519, 65520, -70000, 2049, int.MaxValue, int.MinValue, 255, 12345,
        -2051,
    ];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var halfs = stream.Allocate1D(X);
        using var ints = stream.Allocate1D(Y);
        using var output = stream.Allocate1D<long>(Kernels.NumResults * X.Length);
        stream.Launch((Index1D)X.Length, index => Kernels.HalfIntegerConversionsKernel(index, halfs.View, ints.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
