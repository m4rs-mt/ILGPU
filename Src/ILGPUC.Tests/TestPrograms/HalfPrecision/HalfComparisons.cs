// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfComparisons.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfComparisons
// Half comparisons are ordered floating-point comparisons (false for NaN
// operands, except !=), -0 equals 0, and negative values order below
// positive ones. Each lane prints a bit mask of x < y, x <= y, x > y,
// x >= y, x == y and x != y, followed by a bit mask of the classification
// predicates of x.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal static long Compare(Half x, Half y) =>
        (x < y ? 1 : 0) |
        (x <= y ? 2 : 0) |
        (x > y ? 4 : 0) |
        (x >= y ? 8 : 0) |
        (x == y ? 16 : 0) |
        (x != y ? 32 : 0);

    internal static long Classify(Half x) =>
        (Half.IsNaN(x) ? 1 : 0) |
        (Half.IsInfinity(x) ? 2 : 0) |
        (Half.IsFinite(x) ? 4 : 0) |
        (Half.IsNegative(x) ? 8 : 0) |
        (Half.IsPositive(x) ? 16 : 0) |
        (Half.IsNormal(x) ? 32 : 0) |
        (Half.IsSubnormal(x) ? 64 : 0) |
        (Half.IsPositiveInfinity(x) ? 128 : 0) |
        (Half.IsNegativeInfinity(x) ? 256 : 0) |
        (Half.IsInteger(x) ? 512 : 0) |
        (Half.IsEvenInteger(x) ? 1024 : 0) |
        (Half.IsOddInteger(x) ? 2048 : 0) |
        (XMath.IsNaN(x) ? 4096 : 0) |
        (XMath.IsFinite(x) ? 8192 : 0);

    public static void HalfComparisonsKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> x,
        ArrayView1D<Half, Stride1D.Dense> y,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        output[index * 2] = Compare(x[index], y[index]);
        output[index * 2 + 1] = Classify(x[index]);
    }
}

static class Program
{
    internal static readonly Half[] X =
    [
        (Half)(-2.0f), Half.NegativeZero, (Half)1.0f, (Half)1.0f, Half.NaN, (Half)1.0f,
        Half.PositiveInfinity, Half.NegativeInfinity, (Half)(-3.0f), Half.Epsilon,
        (Half)2.5f, (Half)4.0f,
    ];

    internal static readonly Half[] Y =
    [
        (Half)1.0f, Half.Zero, (Half)1.0f, (Half)(-1.0f), (Half)1.0f, Half.NaN,
        Half.MaxValue, Half.MinValue, (Half)(-2.0f), Half.Zero,
        (Half)2.5f, (Half)(-4.0f),
    ];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var x = stream.Allocate1D(X);
        using var y = stream.Allocate1D(Y);
        using var output = stream.Allocate1D<long>(2 * X.Length);
        stream.Launch((Index1D)X.Length, index => Kernels.HalfComparisonsKernel(index, x.View, y.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
