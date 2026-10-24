// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfVectorPredicates.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfVectorPredicates
// One expression combines all classification predicates of a Float16x8 and of a
// Float32x8 into a bit mask. Every predicate tests the eight lanes with one
// branch each, so the masks of v, -v and (v - 1) * v form a chain of thousands
// of blocks. Each vector prints the three masks, first for Float16x8, then for
// Float32x8.

using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Vectors;

static class Kernels
{
    internal static int Flags(Float16x8 v) =>
        (Float16x8.IsNormal(v) ? 1 : 0) |
        (Float16x8.IsNaN(v) ? 2 : 0) |
        (Float16x8.IsInfinity(v) ? 4 : 0) |
        (Float16x8.IsFinite(v) ? 8 : 0) |
        (Float16x8.IsNegative(v) ? 16 : 0) |
        (Float16x8.IsSubnormal(v) ? 32 : 0) |
        (Float16x8.IsInteger(v) ? 64 : 0) |
        (Float16x8.IsZero(v) ? 128 : 0) |
        (Float16x8.IsPositive(v) ? 256 : 0) |
        (Float16x8.IsEvenInteger(v) ? 512 : 0) |
        (Float16x8.IsOddInteger(v) ? 1024 : 0) |
        (Float16x8.IsNegativeInfinity(v) ? 2048 : 0) |
        (Float16x8.IsPositiveInfinity(v) ? 4096 : 0);

    internal static int Flags(Float32x8 v) =>
        (Float32x8.IsNormal(v) ? 1 : 0) |
        (Float32x8.IsNaN(v) ? 2 : 0) |
        (Float32x8.IsInfinity(v) ? 4 : 0) |
        (Float32x8.IsFinite(v) ? 8 : 0) |
        (Float32x8.IsNegative(v) ? 16 : 0) |
        (Float32x8.IsSubnormal(v) ? 32 : 0) |
        (Float32x8.IsInteger(v) ? 64 : 0) |
        (Float32x8.IsZero(v) ? 128 : 0) |
        (Float32x8.IsPositive(v) ? 256 : 0) |
        (Float32x8.IsEvenInteger(v) ? 512 : 0) |
        (Float32x8.IsOddInteger(v) ? 1024 : 0) |
        (Float32x8.IsNegativeInfinity(v) ? 2048 : 0) |
        (Float32x8.IsPositiveInfinity(v) ? 4096 : 0);

    public static void Float16x8FlagsKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> flags)
    {
        var v = Float16x8.FromElementView(data, index * 8);
        int a = Flags(v);
        int b = Flags(-v);
        int c = Flags((v - Float16x8.One) * v);
        flags[index * 3] = a;
        flags[index * 3 + 1] = b;
        flags[index * 3 + 2] = c;
    }

    public static void Float32x8FlagsKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> flags)
    {
        var v = Float32x8.FromElementView(data, index * 8);
        int a = Flags(v);
        int b = Flags(-v);
        int c = Flags((v - Float32x8.One) * v);
        flags[index * 3] = a;
        flags[index * 3 + 1] = b;
        flags[index * 3 + 2] = c;
    }
}

static class Program
{
    internal static readonly float[] Values =
    [
        1, 2, 3, 4, 5, 6, 7, 8,
        2, -4, 6, -8, 10, -12, 14, -16,
        0.5f, 1.5f, -2.5f, 3.25f, float.NaN, 4, 5, 6,
        0, -0.0f, 0, -0.0f, 0, 0, 0, 0,
        6e-8f, 1e-7f, 3e-6f, 1e-40f, -1e-40f, 2e-5f, -6e-8f, 1e-45f,
        1, float.PositiveInfinity, 3, 4, 5, 6, 7, 8,
        -0.5f, -1.25f, -2.75f, -3.5f, -0.125f, -7.5f, -9.25f, -10.5f,
        float.NegativeInfinity, 1, -1, 2, -2, 3, -3, 0.75f,
        300, -300, 256, 512, 1000, -1000, 400, 700,
        1e20f, -1e20f, 3e38f, 1, 2, 3, 4, 5,
    ];

    internal const int NumVectors = 10;

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        var halfValues = new Half[Values.Length];
        for (int i = 0; i < Values.Length; ++i)
            halfValues[i] = (Half)Values[i];

        using var halfData = stream.Allocate1D(halfValues);
        using var floatData = stream.Allocate1D(Values);
        using var halfFlags = stream.Allocate1D<int>(NumVectors * 3);
        using var floatFlags = stream.Allocate1D<int>(NumVectors * 3);
        stream.Launch((Index1D)NumVectors, index => Kernels.Float16x8FlagsKernel(index, halfData.View, halfFlags.View));
        stream.Launch((Index1D)NumVectors, index => Kernels.Float32x8FlagsKernel(index, floatData.View, floatFlags.View));
        stream.Synchronize();

        foreach (var v in halfFlags.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in floatFlags.GetAsArray1D())
            Console.WriteLine(v);
    }
}
