// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfVectorViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfVectorViews
// Float16x8, Float16x4 and Float16x2 values pass through views of vectors,
// uniform kernel parameters and the SingleVectorView overloads of a
// VectorView<Half>. Each lane computes the Float16x8 arithmetic, decrement,
// magnitude and classification members of its vector and draws a random
// Float16x8 vector. The program prints the bits of every Half result (NaN for
// any NaN), the predicate bit masks and the vectors written through the views.

using System;
using ILGPU;
using ILGPU.Random;
using ILGPU.Runtime;
using ILGPU.Vectors;

static class Kernels
{
    internal const int NumResults = 8;

    internal static Float16x8 Result(Float16x8 v, Float16x2 scale, Float16x4 offset, int k)
    {
        var o = new Float16x8(
            offset.X, offset.Y, offset.Z, offset.W,
            offset.W, offset.Z, offset.Y, offset.X);
        var c = v;
        return k switch
        {
            0 => v * Float16x8.FromScalar(scale.X) + o,
            1 => v / Float16x8.FromScalar(scale.Y) - o,
            2 => Float16x8.Min(v, o) + Float16x8.Max(v, -o),
            3 => Float16x8.Clamp(v, -o, o),
            4 => --c + Float16x8.Abs(v),
            5 => Float16x8.MaxMagnitudeNumber(v / v, o),
            6 => Float16x8.MinMagnitudeNumber(v, o / v),
            _ => Float16x8.MaxMagnitude(v, -o) - Float16x8.MinMagnitude(v, o),
        };
    }

    internal const int NumFlags = 15;

    internal static bool Flag(Float16x8 v, int k)
    {
        var c = v;
        return k switch
        {
            0 => Float16x8.IsNormal(v),
            1 => Float16x8.IsSubnormal(v),
            2 => Float16x8.IsEvenInteger(v),
            3 => Float16x8.IsOddInteger(v),
            4 => Float16x8.IsInfinity(v),
            5 => Float16x8.IsPositiveInfinity(v),
            6 => Float16x8.IsNegativeInfinity(v),
            7 => Float16x8.IsNaN(v),
            8 => Float16x8.IsInteger(v),
            9 => Float16x8.IsFinite(v),
            10 => Float16x8.IsPositive(v),
            11 => Float16x8.IsNegative(v),
            12 => Float16x8.IsZero(v),
            13 => Float16x8.IsOddInteger(--c),
            _ => Float16x8.IsZero(--c + Float16x8.One - v),
        };
    }

    internal static Float16x8 Random(int index)
    {
        var random = new XorShift32((uint)(index * 7919 + 13));
        return Float16x8.GetRandom(
            ref random,
            Float16x8.FromScalar((Half)(-2.0f)),
            new Float16x8(
                (Half)2.0f, (Half)4.0f, (Half)1.0f, (Half)8.0f,
                (Half)0.5f, (Half)2.0f, (Half)16.0f, (Half)(-1.0f)));
    }

    public static void HalfVectorViewsKernel(
        Index1D index,
        ArrayView1D<Float16x8, Stride1D.Dense> data,
        ArrayView1D<Float16x8, Stride1D.Dense> results,
        ArrayView1D<Float16x4, Stride1D.Dense> quads,
        ArrayView1D<Float16x2, Stride1D.Dense> pairs,
        Float16x2 scale,
        Float16x4 offset,
        VectorView<Half> columns,
        ArrayView1D<long, Stride1D.Dense> flags)
    {
        var v = data[index];
        for (int k = 0; k < NumResults; ++k)
            results[index * NumResults + k] = Result(v, scale, offset, k);
        data[index] = Random(index);
        long mask = 0;
        for (int k = 0; k < NumFlags; ++k)
            mask |= Flag(v, k) ? 1L << k : 0L;
        flags[index] = mask;

        var column = columns.SliceVector(index);
        var w = Float16x8.FromElementView(column, 0);
        var q = Float16x4.FromElementView(column, 2);
        var p = Float16x2.FromElementView(column, 6);
        (w + Float16x8.One).ToElementView(column, 0);
        (q * offset).ToElementView(column, 1);
        (p - scale).ToElementView(column, 6);
        quads[index] = q + offset;
        pairs[index] = new Float16x2(w.X, w.L) * scale;
    }
}

static class Program
{
    internal static Float16x8[] Inputs =>
    [
        new((Half)1, (Half)2, (Half)3, (Half)4, (Half)5, (Half)6, (Half)7, (Half)8),
        new((Half)2, (Half)4, (Half)6, (Half)8, (Half)(-2), (Half)(-4), (Half)(-6), (Half)(-8)),
        new((Half)1, (Half)3, (Half)5, (Half)7, (Half)(-1), (Half)(-3), (Half)(-5), (Half)(-7)),
        new((Half)0.5f, (Half)1.5f, (Half)2.25f, (Half)3, (Half)0.125f, (Half)6, (Half)7.5f, (Half)100),
        new((Half)(-0.5f), (Half)(-1), (Half)(-2.5f), (Half)(-3), (Half)(-0.75f), (Half)(-6), (Half)(-7), (Half)(-1000)),
        new(Half.Zero, Half.NegativeZero, Half.Zero, Half.Zero, Half.Zero, Half.Zero, Half.Zero, Half.Zero),
        new(Half.Epsilon, (Half)1e-6f, (Half)2e-6f, (Half)3e-6f, (Half)(-1e-6f), (Half)4e-6f, (Half)5e-6f, (Half)6e-5f),
        new(Half.PositiveInfinity, (Half)1, Half.NaN, (Half)(-3), Half.NegativeInfinity, (Half)0.5f, (Half)2, Half.MaxValue),
        new(Half.PositiveInfinity, Half.PositiveInfinity, Half.PositiveInfinity, Half.PositiveInfinity, Half.PositiveInfinity, Half.PositiveInfinity, Half.PositiveInfinity, Half.PositiveInfinity),
        new(Half.MaxValue, Half.MinValue, (Half)1024, (Half)2047, (Half)(-2049), (Half)4096, (Half)(-1), (Half)1),
    ];

    internal static void Print(Half value) =>
        Console.WriteLine(Half.IsNaN(value)
            ? "NaN"
            : BitConverter.HalfToUInt16Bits(value).ToString());

    internal static void Print(ReadOnlySpan<Half> values)
    {
        foreach (var value in values)
            Print(value);
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        var inputs = Inputs;
        int numLanes = inputs.Length;
        var columnData = new Half[numLanes * 8];
        for (int i = 0; i < columnData.Length; ++i)
            columnData[i] = (Half)((i * 3 % 17) * 0.5f - 4.0f);

        using var data = stream.Allocate1D(inputs);
        using var results = stream.Allocate1D<Float16x8>(numLanes * Kernels.NumResults);
        using var quads = stream.Allocate1D<Float16x4>(numLanes);
        using var pairs = stream.Allocate1D<Float16x2>(numLanes);
        using var columns = stream.Allocate1D(columnData);
        using var flags = stream.Allocate1D<long>(numLanes);
        var scale = new Float16x2((Half)1.5f, (Half)(-0.25f));
        var offset = new Float16x4((Half)0.5f, (Half)(-2), (Half)3, (Half)0.125f);
        var columnView = new VectorView<Half>(
            columns.View.As2DDenseYView(new LongIndex2D(8, numLanes)));
        stream.Launch((Index1D)numLanes, index => Kernels.HalfVectorViewsKernel(index, data.View, results.View, quads.View, pairs.View, scale, offset, columnView, flags.View));
        stream.Synchronize();

        foreach (var v in results.GetAsArray1D())
            Print(v.AsSpan());
        foreach (var v in flags.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in data.GetAsArray1D())
            Print(v.AsSpan());
        foreach (var v in quads.GetAsArray1D())
            Print(v.AsSpan());
        foreach (var v in pairs.GetAsArray1D())
            Print(v.AsSpan());
        Print(columns.GetAsArray1D());
    }
}
