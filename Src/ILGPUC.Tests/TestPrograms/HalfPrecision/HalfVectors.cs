// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfVectors.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfVectors
// The Float16 vector types load from and store to Half views and compute
// their arithmetic, min/max, magnitude, increment and generic operations per
// element, like the Float32 vector types. Each lane prints the bits of the
// elements of every Float16x4 and Float16x2 result, the predicate masks
// (including IsZero) and the Float32x4 results times 1000.

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Vectors;

static class Kernels
{
    internal const int NumResults = 9;

    internal static T Polynomial<T>(T x, T y) where T : INumberBase<T> =>
        x * y + x - T.One;

    internal static Float16x4 Result(Float16x4 a, Float16x4 b, int k)
    {
        var c = a;
        return k switch
        {
            0 => a + b,
            1 => a - b * a,
            2 => a / b,
            3 => Float16x4.Min(a, b) + Float16x4.Max(a, -b),
            4 => Float16x4.Abs(a) + Float16x4.Clamp(
                a, Float16x4.FromScalar(Half.NegativeOne), Float16x4.One),
            5 => Float16x4.MaxMagnitude(a, b) - Float16x4.MinMagnitude(a, b),
            6 => ++c + Float16x4.FromScalar((Half)0.5f),
            7 => Polynomial(a, b),
            _ => -a + Float16x4.One,
        };
    }

    internal static long Flags(Float16x4 a) =>
        (Float16x4.IsNegative(a) ? 1 : 0) |
        (Float16x4.IsInteger(a) ? 2 : 0) |
        (Float16x4.IsZero(a - a) ? 4 : 0) |
        (Float16x4.IsFinite(a) ? 8 : 0) |
        (Float16x4.IsNaN(a / a) ? 16 : 0) |
        (Float16x4.IsZero(a) ? 32 : 0);

    internal static Float16x2 Pair(Float16x4 a, Float16x4 b) =>
        Polynomial(new Float16x2(a.X, a.Y), new Float16x2(b.Z, b.W));

    internal static Float32x4 Single(Float16x4 a, Float16x4 b)
    {
        var x = new Float32x4((float)a.X, (float)a.Y, (float)a.Z, (float)a.W);
        var y = new Float32x4((float)b.X, (float)b.Y, (float)b.Z, (float)b.W);
        return Polynomial(x, y);
    }

    public static void HalfVectorsKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> a,
        ArrayView1D<Half, Stride1D.Dense> b,
        ArrayView1D<Half, Stride1D.Dense> results,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var x = Float16x4.FromElementView(a, index * 4);
        var y = Float16x4.FromElementView(b, index * 4);
        for (int k = 0; k < NumResults; ++k)
            Result(x, y, k).ToElementView(results, (index * NumResults + k) * 4);

        var pair = Pair(x, y);
        var single = Single(x, y);
        output[index * 7] = Flags(x);
        output[index * 7 + 1] = BitConverter.HalfToUInt16Bits(pair.X);
        output[index * 7 + 2] = BitConverter.HalfToUInt16Bits(pair.Y);
        output[index * 7 + 3] = (long)(single.X * 1000.0f);
        output[index * 7 + 4] = (long)(single.Y * 1000.0f);
        output[index * 7 + 5] = (long)(single.Z * 1000.0f);
        output[index * 7 + 6] = (long)(single.W * 1000.0f);
    }
}

static class Program
{
    internal const int NumLanes = 12;

    internal static Half[] CreateInput(int scale, int modulus, float step, float offset)
    {
        var values = new Half[NumLanes * 4];
        for (int i = 0; i < values.Length; ++i)
            values[i] = (Half)((i * scale % modulus) * step + offset);
        return values;
    }

    internal static readonly Half[] A = CreateInput(1, 7, 0.5f, -1.5f);
    internal static readonly Half[] B = CreateInput(3, 5, 0.75f, 0.25f);

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var a = stream.Allocate1D(A);
        using var b = stream.Allocate1D(B);
        using var results = stream.Allocate1D<Half>(NumLanes * Kernels.NumResults * 4);
        using var output = stream.Allocate1D<long>(NumLanes * 7);
        stream.Launch((Index1D)NumLanes, index => Kernels.HalfVectorsKernel(index, a.View, b.View, results.View, output.View));
        stream.Synchronize();

        foreach (var v in results.GetAsArray1D())
            Console.WriteLine(BitConverter.HalfToUInt16Bits(v));
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
