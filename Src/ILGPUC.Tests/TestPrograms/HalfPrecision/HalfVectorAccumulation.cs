// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfVectorAccumulation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfVectorAccumulation
// The Float16 vector types accumulate Float16, byte and sbyte vectors, and the
// Float32 vector types accumulate Float16 vectors. 256 threads atomically add
// to one element of each Float16 vector view (both halves of every 32-bit word
// at once) and of a Float32x4 view; all partial sums are exact, so the sums
// do not depend on the order of the additions. Each lane of the second kernel
// prints the bits of the mixed +/- results, the averages and the results of
// the generic accumulation interface, and the Float32x8 results times 1000.

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Vectors;

static class Kernels
{
    internal const int NumResults = 10;

    internal static Float16x8 Step => new(
        (Half)1.0f, (Half)0.5f, (Half)2.0f, (Half)(-1.0f),
        (Half)0.25f, (Half)3.0f, (Half)(-0.5f), (Half)4.0f);

    public static void HalfVectorAtomicsKernel(
        Index1D index,
        ArrayView1D<Float16x8, Stride1D.Dense> sums8,
        ArrayView1D<Float16x4, Stride1D.Dense> sums4,
        ArrayView1D<Float16x2, Stride1D.Dense> sums2,
        ArrayView1D<Float32x4, Stride1D.Dense> wide)
    {
        int i = index;
        var step = Step;
        Float16x8.AtomicAdd(ref sums8[0], step);
        Float16x8.AtomicAdd(ref sums8[1 + i % 2], -step);
        Float16x4.AtomicAdd(ref sums4[0], new Float16x4(step.X, step.Y, step.Z, step.W));
        Float16x4.AtomicAdd(ref sums4[1], new Int8x4(1, -2, 3, (sbyte)(i % 4 - 2)));
        Float16x2.AtomicAdd(ref sums2[i % 2], new UInt8x2(1, 2));
        Float16x2.AtomicAdd(ref sums2[2], new Float16x2(step.I, step.L));
        Float32x4.AtomicAdd(ref wide[0], Float16x4.FromScalar((Half)0.1f));
        Float32x4.AtomicAdd(ref wide[1], new Float16x4(
            (Half)i, (Half)(-0.5f), Half.One, (Half)(i % 2)));
    }

    internal static TAcc Average<TAcc, TOther, TElement>(
        TOther first,
        TOther second,
        TElement denominator)
        where TAcc : unmanaged, IAccumulationVectorType<TAcc, TOther, TElement>
        where TOther : unmanaged, IVectorType<TOther, TElement>
        where TElement : unmanaged, INumber<TElement> =>
        (TAcc.ConvertFromBase(first) + second).ComputeAverage(denominator);

    internal static TOther Truncate<TAcc, TOther, TElement>(TAcc value)
        where TAcc : unmanaged, IAccumulationVectorType<TAcc, TOther, TElement>
        where TOther : unmanaged, IVectorType<TOther, TElement>
        where TElement : unmanaged, INumber<TElement> =>
        TAcc.ConvertToBase(value);

    internal static Float16x8 Result(Float16x8 x, Int8x8 s, UInt8x8 u, int k) => k switch
    {
        0 => x + s,
        1 => s - x,
        2 => x - u,
        3 => u + x - s,
        4 => x.ComputeAverage(16L),
        5 => x.ComputeAverage((Half)4.0f),
        6 => x.ComputeAverage((byte)8),
        7 => x.ComputeAverage((sbyte)(-2)),
        8 => Average<Float16x8, Int8x8, sbyte>(s, s, (sbyte)2) - x,
        _ => Average<Float16x8, Float16x8, Half>(x, x, (Half)2.0f),
    };

    internal static Float32x8 Wide(Float16x8 x, int k) => k switch
    {
        0 => Float32x8.FromScalar(1.5f) + x,
        1 => x - Float32x8.FromScalar(0.25f),
        2 => Average<Float32x8, Float16x8, Half>(x, x, (Half)0.5f),
        _ => Float32x8.FromScalar(3.0f).ComputeAverage((Half)8.0f) - x,
    };

    internal static Int8x8 Narrow(Float16x8 x) =>
        Truncate<Float16x8, Int8x8, sbyte>(x * Float16x8.FromScalar((Half)4.0f));

    public static void HalfVectorAccumulationKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> a,
        ArrayView1D<sbyte, Stride1D.Dense> b,
        ArrayView1D<Half, Stride1D.Dense> results,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var x = Float16x8.FromElementView(a, index * 8);
        var s = Int8x8.FromElementView(b, index * 8);
        var u = new UInt8x8(
            (byte)index.X, 1, 2, 3, 4, 5, 6, (byte)(index.X * 3));
        for (int k = 0; k < NumResults; ++k)
            Result(x, s, u, k).ToElementView(results, (index * NumResults + k) * 8);

        int offset = index * 40;
        for (int k = 0; k < 4; ++k)
        {
            var w = Wide(x, k);
            output[offset++] = (long)(w.X * 1000.0f);
            output[offset++] = (long)(w.Y * 1000.0f);
            output[offset++] = (long)(w.Z * 1000.0f);
            output[offset++] = (long)(w.W * 1000.0f);
            output[offset++] = (long)(w.I * 1000.0f);
            output[offset++] = (long)(w.J * 1000.0f);
            output[offset++] = (long)(w.K * 1000.0f);
            output[offset++] = (long)(w.L * 1000.0f);
        }
        var n = Narrow(x);
        output[offset++] = n.X;
        output[offset++] = n.Y;
        output[offset++] = n.Z;
        output[offset++] = n.W;
        output[offset++] = n.I;
        output[offset++] = n.J;
        output[offset++] = n.K;
        output[offset] = n.L;
    }
}

static class Program
{
    internal const int NumThreads = 256;
    internal const int NumLanes = 6;

    internal static Half[] CreateInput()
    {
        var values = new Half[NumLanes * 8];
        for (int i = 0; i < values.Length; ++i)
            values[i] = (Half)((i * 5 % 11) * 0.75f - 3.25f);
        return values;
    }

    internal static sbyte[] CreateIntegers()
    {
        var values = new sbyte[NumLanes * 8];
        for (int i = 0; i < values.Length; ++i)
            values[i] = (sbyte)(i * 7 % 13 - 6);
        return values;
    }

    internal static void Print(Half value) =>
        Console.WriteLine(BitConverter.HalfToUInt16Bits(value));

    internal static void Print(Float16x8 value)
    {
        foreach (var element in value.AsSpan())
            Print(element);
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var sums8 = stream.Allocate1D<Float16x8>(3);
        using var sums4 = stream.Allocate1D<Float16x4>(2);
        using var sums2 = stream.Allocate1D<Float16x2>(3);
        using var wide = stream.Allocate1D<Float32x4>(2);
        sums8.MemSetToZero();
        sums4.MemSetToZero();
        sums2.MemSetToZero();
        wide.MemSetToZero();
        stream.Launch((Index1D)NumThreads, index => Kernels.HalfVectorAtomicsKernel(index, sums8.View, sums4.View, sums2.View, wide.View));

        using var a = stream.Allocate1D(CreateInput());
        using var b = stream.Allocate1D(CreateIntegers());
        using var results = stream.Allocate1D<Half>(NumLanes * Kernels.NumResults * 8);
        using var output = stream.Allocate1D<long>(NumLanes * 40);
        stream.Launch((Index1D)NumLanes, index => Kernels.HalfVectorAccumulationKernel(index, a.View, b.View, results.View, output.View));
        stream.Synchronize();

        foreach (var v in sums8.GetAsArray1D())
            Print(v);
        foreach (var v in sums4.GetAsArray1D())
        {
            Print(v.X);
            Print(v.Y);
            Print(v.Z);
            Print(v.W);
        }
        foreach (var v in sums2.GetAsArray1D())
        {
            Print(v.X);
            Print(v.Y);
        }
        foreach (var v in wide.GetAsArray1D())
        {
            Console.WriteLine((long)(v.X * 100000.0f));
            Console.WriteLine((long)(v.Y * 100000.0f));
            Console.WriteLine((long)(v.Z * 100000.0f));
            Console.WriteLine((long)(v.W * 100000.0f));
        }
        foreach (var v in results.GetAsArray1D())
            Print(v);
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
