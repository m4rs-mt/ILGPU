// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfMath.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfMath
// The System.Half math functions and the XMath Half overloads evaluate in
// single precision and round to half precision, like System.Half on the
// host. Each lane prints the bits of every function result for its inputs
// x and y (-1 for NaN, whose bit pattern differs between the backends).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 60;

    internal static Half Result(Half x, Half y, int k)
    {
        // Inputs within the domains of the inverse and logarithmic functions
        var s = (Half)((float)x / 16.0f);
        var a = Half.Abs(x) + Half.One;
        var h = x;
        return k switch
        {
            0 => Half.Sin(x),
            1 => Half.Cos(x),
            2 => Half.Tan(s),
            3 => Half.Asin(s),
            4 => Half.Acos(s),
            5 => Half.Atan(x),
            6 => Half.Atan2(x, y),
            7 => Half.Sinh(s),
            8 => Half.Cosh(s),
            9 => Half.Tanh(x),
            10 => Half.Asinh(x),
            11 => Half.Acosh(a),
            12 => Half.Atanh(s),
            13 => Half.Exp(s),
            14 => Half.Exp2(s),
            15 => Half.Exp10(s),
            16 => Half.Log(a),
            17 => Half.Log2(a),
            18 => Half.Log10(a),
            19 => Half.Log(a, (Half)3.0f),
            20 => Half.Pow(a, y),
            21 => Half.Sqrt(a),
            22 => Half.Cbrt(x),
            23 => XMath.Rsqrt(a),
            24 => Half.Floor(x),
            25 => Half.Ceiling(x),
            26 => Half.Round(x),
            27 => Half.Round(x, 1),
            28 => Half.Round(x, MidpointRounding.AwayFromZero),
            29 => Half.Truncate(x),
            30 => Half.Abs(x),
            31 => Half.Min(x, y),
            32 => Half.Max(x, y),
            33 => Half.Clamp(x, Half.NegativeOne, Half.One),
            34 => Half.CopySign(x, y),
            35 => Half.FusedMultiplyAdd(x, y, s),
            36 => Half.Ieee754Remainder(x, y),
            37 => x % y,
            38 => Half.Hypot(x, y),
            39 => Half.Lerp(x, y, (Half)0.25f),
            40 => Half.DegreesToRadians(x),
            41 => Half.RadiansToDegrees(s),
            42 => Half.MaxMagnitude(x, y),
            43 => Half.MinMagnitude(x, y),
            44 => Half.MaxNumber(x, y),
            45 => Half.MinNumber(x, y),
            46 => (Half)Half.Sign(x),
            47 => ++h,
            48 => --h,
            49 => +x,
            50 => -x,
            51 => XMath.Sin(x),
            52 => XMath.Sqrt(a),
            53 => XMath.Exp2(s),
            54 => Half.SinCos(x).Sin,
            55 => Half.SinCos(x).Cos,
            56 => x * y,
            57 => x / y,
            58 => x + y,
            _ => x - y,
        };
    }

    internal static long Bits(Half value) =>
        Half.IsNaN(value) ? -1 : BitConverter.HalfToUInt16Bits(value);

    public static void HalfMathKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> x,
        ArrayView1D<Half, Stride1D.Dense> y,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        for (int k = 0; k < NumResults; ++k)
            output[index * NumResults + k] = Bits(Result(x[index], y[index], k));
    }
}

static class Program
{
    internal static readonly Half[] X =
    [
        (Half)0.25f, (Half)0.5f, (Half)0.75f, (Half)1.5f, (Half)2.0f, (Half)3.0f,
        (Half)(-0.5f), (Half)(-1.25f), (Half)10.0f, (Half)0.1f, (Half)5.5f, (Half)(-7.0f),
    ];

    internal static readonly Half[] Y =
    [
        (Half)2.0f, (Half)(-1.5f), (Half)0.5f, (Half)4.0f, (Half)(-3.0f), (Half)0.75f,
        (Half)1.0f, (Half)2.5f, (Half)(-0.25f), (Half)3.0f, (Half)(-2.0f), (Half)1.5f,
    ];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var x = stream.Allocate1D(X);
        using var y = stream.Allocate1D(Y);
        using var output = stream.Allocate1D<long>(Kernels.NumResults * X.Length);
        stream.Launch((Index1D)X.Length, index => Kernels.HalfMathKernel(index, x.View, y.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
