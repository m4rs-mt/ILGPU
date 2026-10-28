// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfFunctions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfFunctions
// The System.Half functions without a float counterpart in XMath (the pi
// functions, ExpM1 and LogP1 families, RootN, ScaleB, ILogB, BitIncrement,
// the native and magnitude-number variants of Min/Max/Clamp), Min/Max/Clamp
// with NaN and signed zeros, the bitwise operators, and CompareTo, Equals and
// GetHashCode match System.Half on the host for zeros, subnormal values,
// infinities and NaN. Five straight-line kernels, one per group of functions,
// store the results for the inputs x and y of a lane in its slots of one
// output, which prints every result of each lane in order (Half results as
// bits, -1 for NaN, whose bit pattern differs between the backends).

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 52;

    static T And<T>(T x, T y) where T : IBitwiseOperators<T, T, T> => x & y;
    static T Or<T>(T x, T y) where T : IBitwiseOperators<T, T, T> => x | y;
    static T Xor<T>(T x, T y) where T : IBitwiseOperators<T, T, T> => x ^ y;
    static T Not<T>(T x) where T : IBitwiseOperators<T, T, T> => ~x;

    internal static long Bits(Half value) =>
        Half.IsNaN(value) ? -1 : BitConverter.HalfToUInt16Bits(value);

    public static void PiFunctionsKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> xs,
        ArrayView1D<Half, Stride1D.Dense> ys,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var x = xs[index];
        var y = ys[index];
        // Inputs within the domains of the inverse functions
        var s = (Half)((float)x / 16.0f);
        var i = index * NumResults;
        output[i + 0] = Bits(Half.SinPi(x));
        output[i + 1] = Bits(Half.CosPi(x));
        output[i + 2] = Bits(Half.TanPi(x));
        output[i + 3] = Bits(Half.SinCosPi(x).SinPi);
        output[i + 4] = Bits(Half.SinCosPi(x).CosPi);
        output[i + 5] = Bits(Half.AsinPi(s));
        output[i + 6] = Bits(Half.AcosPi(s));
        output[i + 7] = Bits(Half.AtanPi(x));
        output[i + 8] = Bits(Half.Atan2Pi(x, y));
    }

    public static void ExpM1LogP1Kernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> xs,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var x = xs[index];
        // Inputs within the domains of the logarithmic functions
        var e = (Half)((float)x / 16.0f + 0.3f);
        var a = Half.Abs(x);
        var i = index * NumResults;
        output[i + 9] = Bits(Half.ExpM1(e));
        output[i + 10] = Bits(Half.Exp2M1(e));
        output[i + 11] = Bits(Half.Exp10M1(e));
        output[i + 12] = Bits(Half.LogP1(a));
        output[i + 13] = Bits(Half.Log2P1(a));
        output[i + 14] = Bits(Half.Log10P1(a));
    }

    public static void RootScaleKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> xs,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var x = xs[index];
        var i = index * NumResults;
        output[i + 15] = Bits(Half.RootN(x, 2));
        output[i + 16] = Bits(Half.RootN(x, -3));
        output[i + 17] = Bits(Half.RootN(x, 4));
        output[i + 18] = Bits(Half.RootN(x, 0));
        output[i + 19] = Bits(Half.ScaleB(x, 3));
        output[i + 20] = Bits(Half.ScaleB(x, -20));
        output[i + 21] = Half.ILogB(x);
        output[i + 22] = Bits(Half.BitIncrement(x));
        output[i + 23] = Bits(Half.BitDecrement(x));
        output[i + 24] = Half.IsPow2(x) ? 1 : 0;
        output[i + 25] = Half.IsRealNumber(x) ? 1 : 0;
        output[i + 50] = Bits(Half.ScaleB(x, 30));
    }

    public static void MinMaxKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> xs,
        ArrayView1D<Half, Stride1D.Dense> ys,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var x = xs[index];
        var y = ys[index];
        var i = index * NumResults;
        output[i + 26] = Bits(Half.Max(x, y));
        output[i + 27] = Bits(Half.Min(x, y));
        output[i + 28] = Bits(Half.Clamp(x, Half.NegativeOne, Half.One));
        output[i + 29] = Bits(Half.Clamp(x, y, Half.PositiveInfinity));
        output[i + 30] = Bits(Half.MaxNative(x, y));
        output[i + 31] = Bits(Half.MinNative(x, y));
        output[i + 32] = Bits(Half.ClampNative(x, Half.NegativeOne, Half.One));
        output[i + 33] = Bits(Half.MaxMagnitudeNumber(x, y));
        output[i + 34] = Bits(Half.MinMagnitudeNumber(x, y));
        output[i + 44] = Bits(Half.Max(y, x));
        output[i + 45] = Bits(Half.Min(y, x));
        output[i + 46] = Bits(Half.MaxNumber(x, y));
        output[i + 47] = Bits(Half.MinNumber(x, y));
        output[i + 48] = Bits(Half.MaxMagnitude(x, y));
        output[i + 49] = Bits(Half.MinMagnitude(x, y));
    }

    public static void BitwiseCompareKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> xs,
        ArrayView1D<Half, Stride1D.Dense> ys,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var x = xs[index];
        var y = ys[index];
        var s = (Half)((float)x / 16.0f);
        var i = index * NumResults;
        output[i + 35] = Bits(Half.MultiplyAddEstimate(x, y, s));
        output[i + 36] = Bits(Half.Hypot(x, y));
        output[i + 37] = Bits(And(x, y));
        output[i + 38] = Bits(Or(x, Half.One));
        output[i + 39] = Bits(Xor(x, y));
        output[i + 40] = Bits(Not(Not(x)));
        output[i + 41] = x.CompareTo(y);
        output[i + 42] = x.Equals(y) ? 1 : 0;
        output[i + 43] = x.GetHashCode();
        output[i + 51] = Bits(Half.Hypot(y, x));
    }
}

static class Program
{
    internal static readonly Half[] X =
    [
        (Half)0.25f, (Half)(-0.5f), (Half)1.5f, (Half)(-2.0f), (Half)3.0f, (Half)0.75f,
        Half.NaN, Half.PositiveInfinity, Half.NegativeInfinity, Half.NegativeZero,
        Half.Zero, BitConverter.UInt16BitsToHalf(0x0003), (Half)1000.0f,
        BitConverter.UInt16BitsToHalf(0x8204), (Half)0.1f, (Half)2.5f,
    ];

    internal static readonly Half[] Y =
    [
        (Half)2.0f, Half.NaN, (Half)(-0.5f), (Half)1.0f, Half.NegativeZero, (Half)0.5f,
        (Half)1.0f, Half.NaN, (Half)2.0f, Half.Zero, Half.NegativeZero,
        Half.PositiveInfinity, (Half)(-3.0f), (Half)0.125f, Half.NegativeInfinity,
        (Half)(-2.5f),
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
        stream.Launch((Index1D)X.Length, index => Kernels.PiFunctionsKernel(index, x.View, y.View, output.View));
        stream.Launch((Index1D)X.Length, index => Kernels.ExpM1LogP1Kernel(index, x.View, output.View));
        stream.Launch((Index1D)X.Length, index => Kernels.RootScaleKernel(index, x.View, output.View));
        stream.Launch((Index1D)X.Length, index => Kernels.MinMaxKernel(index, x.View, y.View, output.View));
        stream.Launch((Index1D)X.Length, index => Kernels.BitwiseCompareKernel(index, x.View, y.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
