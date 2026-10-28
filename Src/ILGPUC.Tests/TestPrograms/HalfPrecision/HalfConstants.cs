// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfConstants
// The System.Half constants have their IEEE 754 binary16 bit patterns in
// kernels, and they combine with lane values (#1606: One was 0x0001).
// Each lane prints the bits of the fourteen constants, then the bits of
// x + One, One / x and Rcp(x) for x = lane + 1.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 17;

    internal static Half Constant(int i) => i switch
    {
        0 => Half.Zero,
        1 => Half.NegativeZero,
        2 => Half.One,
        3 => Half.NegativeOne,
        4 => Half.Epsilon,
        5 => Half.MaxValue,
        6 => Half.MinValue,
        7 => Half.NaN,
        8 => Half.PositiveInfinity,
        9 => Half.NegativeInfinity,
        10 => Half.E,
        11 => Half.Pi,
        12 => Half.Tau,
        _ => Half.MultiplicativeIdentity,
    };

    internal static long Result(int lane, int k)
    {
        var x = (Half)(lane + 1);
        var value = k switch
        {
            14 => x + Half.One,
            15 => Half.One / x,
            16 => XMath.Rcp(x),
            _ => Constant(k),
        };
        return BitConverter.HalfToUInt16Bits(value);
    }

    public static void HalfConstantsKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output)
    {
        for (int k = 0; k < NumResults; ++k)
            output[index * NumResults + k] = Result(index, k);
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

        using var output = stream.Allocate1D<long>(2 * Kernels.NumResults);
        stream.Launch((Index1D)2, index => Kernels.HalfConstantsKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
