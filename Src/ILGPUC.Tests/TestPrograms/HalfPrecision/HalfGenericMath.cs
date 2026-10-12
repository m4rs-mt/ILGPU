// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfGenericMath.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfGenericMath
// Generic math over INumber<T>, IBinaryNumber<T> and the floating-point
// function interfaces dispatches to the System.Half implementations when T is
// Half, including the explicit interface members (AdditiveIdentity, IsZero,
// AllBitsSet, the bitwise operators) and the generic conversions. Each lane
// prints the bits of the Half results and, for comparison, the float results
// scaled by 1000.

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static T Polynomial<T>(T x) where T : INumber<T> =>
        x * x - T.One + T.MultiplicativeIdentity * x;

    static T Magnitude<T>(T x, T y) where T : IRootFunctions<T>, INumber<T> =>
        T.Sqrt(x * x + y * y);

    static T Wave<T>(T x) where T : ITrigonometricFunctions<T>, INumber<T> =>
        T.Sin(x) + T.Cos(x);

    static T Largest<T>(T x, T y) where T : INumber<T> =>
        T.Max(T.Abs(x), T.Abs(y)) + T.Zero;

    static T Offset<T>(T x) where T : INumberBase<T> => x + T.AdditiveIdentity;

    static long Classify<T>(T x) where T : INumberBase<T> =>
        (T.IsZero(x) ? 1 : 0) |
        (T.IsCanonical(x) ? 2 : 0) |
        (T.IsRealNumber(x) ? 4 : 0) |
        (T.IsComplexNumber(x) ? 8 : 0) |
        (T.IsImaginaryNumber(x) ? 16 : 0) |
        (T.Radix == 2 ? 32 : 0);

    static T Masked<T>(T x) where T : IBinaryNumber<T> => x & T.AllBitsSet;

    static bool IsPow2<T>(T x) where T : IBinaryNumber<T> => T.IsPow2(x);

    static int Saturated<T>(T x) where T : INumberBase<T> => int.CreateSaturating(x);

    static T FromInt<T>(int value) where T : INumberBase<T> => T.CreateChecked(value);

    public static void HalfGenericMathKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var h = (Half)(index * 0.25f - 1.0f);
        var f = index * 0.25f - 1.0f;
        output[index * 14] = BitConverter.HalfToUInt16Bits(Polynomial(h));
        output[index * 14 + 1] = BitConverter.HalfToUInt16Bits(Magnitude(h, Half.One));
        output[index * 14 + 2] = BitConverter.HalfToUInt16Bits(Wave(h));
        output[index * 14 + 3] = BitConverter.HalfToUInt16Bits(Largest(h, (Half)0.5f));
        output[index * 14 + 4] = (long)(Polynomial(f) * 1000);
        output[index * 14 + 5] = (long)(Magnitude(f, 1.0f) * 1000);
        output[index * 14 + 6] = (long)(Wave(f) * 1000);
        output[index * 14 + 7] = (long)(Largest(f, 0.5f) * 1000);
        output[index * 14 + 8] = BitConverter.HalfToUInt16Bits(Offset(h));
        output[index * 14 + 9] = Classify(h) | Classify(f) << 8;
        output[index * 14 + 10] = BitConverter.HalfToUInt16Bits(Masked(h));
        output[index * 14 + 11] = (IsPow2(h) ? 1 : 0) | (IsPow2(f) ? 2 : 0);
        output[index * 14 + 12] = Saturated(h * (Half)100.0f);
        output[index * 14 + 13] = BitConverter.HalfToUInt16Bits(FromInt<Half>(index * 7 - 30));
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

        using var output = stream.Allocate1D<long>(14 * 12);
        stream.Launch((Index1D)12, index => Kernels.HalfGenericMathKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
