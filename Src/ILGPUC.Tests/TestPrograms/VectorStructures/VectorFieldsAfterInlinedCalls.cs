// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: VectorFieldsAfterInlinedCalls.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: VectorFieldsAfterInlinedCalls
// A vector passed by value to a helper inside a loop keeps its fields for a
// struct-returning helper after the loop: the vector's load is created in
// the block of its inlined constructor, not in the first block that uses it.
// IsZero on Float32x4, Int32x4, FixedInt2DPx4 and FixedLong4DPx2 compares every
// element with zero. Each lane prints pair.X, pair.Y and the IsZero bit mask,
// followed by the loop's results.
// Expected output: 0 40 127 5 80 45 9 120 45 13 160 45 (pair.X, pair.Y and the
// mask per lane), then the loop's results 0 0 0 0 -0 -0 -0 -0 10 12 14 16
// -5 -6 -7 -8 18 20 22 24 -9 -10 -11 -12 26 28 30 32 -13 -14 -15 -16

using System;
using ILGPU;
using ILGPU.FixedPrecision;
using ILGPU.Runtime;
using ILGPU.Vectors;

static class Kernels
{
    internal static Float32x4 Result(Float32x4 a, int k) => k == 0 ? a + a : -a;

    internal static Float32x2 Pair(Float32x4 a, Float32x4 b) =>
        new Float32x2(a.X, b.W);

    internal static int ZeroMask(Float32x4 a) =>
        (Float32x4.IsZero(a - a) ? 1 : 0) |
        (Float32x4.IsZero(a) ? 2 : 0) |
        (Float32x4.IsZero(-Float32x4.Zero) ? 4 : 0) |
        (Int32x4.IsZero(new Int32x4((int)a.X - (int)a.X, 0, 0, 0)) ? 8 : 0) |
        (Int32x4.IsZero(new Int32x4(0, 0, 0, (int)a.W)) ? 16 : 0) |
        (FixedInt2DPx4.IsZero(new FixedInt2DPx4(
            new FixedInt2DP((int)a.X - (int)a.X), default, default, default)) ? 32 : 0) |
        (FixedLong4DPx2.IsZero(new FixedLong4DPx2(
            default, new FixedLong4DP((long)a.Z))) ? 64 : 0);

    public static void VectorFieldsAfterInlinedCallsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> a,
        ArrayView1D<float, Stride1D.Dense> b,
        ArrayView1D<float, Stride1D.Dense> results,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        var x = Float32x4.FromElementView(a, index * 4);
        var y = Float32x4.FromElementView(b, index * 4);
        for (int k = 0; k < 2; ++k)
            Result(x, k).ToElementView(results, (index * 2 + k) * 4);
        var pair = Pair(x, y);
        output[index * 3] = pair.X;
        output[index * 3 + 1] = pair.Y;
        output[index * 3 + 2] = ZeroMask(x);
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

        var av = new float[16];
        var bv = new float[16];
        for (int i = 0; i < av.Length; ++i)
        {
            av[i] = i < 4 ? 0.0f : i + 1;
            bv[i] = 10 * (i + 1);
        }
        using var a = stream.Allocate1D(av);
        using var b = stream.Allocate1D(bv);
        using var results = stream.Allocate1D<float>(32);
        using var output = stream.Allocate1D<float>(12);
        stream.Launch((Index1D)4, index => Kernels.VectorFieldsAfterInlinedCallsKernel(index, a.View, b.View, results.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in results.GetAsArray1D())
            Console.WriteLine(v);
    }
}
