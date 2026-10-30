// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfDivisionByConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfDivisionByConstants
// Divides every Half value by constants (powers of two, subnormal values, the
// largest finite value and values that are not powers of two) in a kernel and
// compares the bits of the quotients with System.Half on the host. Prints the
// number of differing quotients per constant, which is zero with fusion
// turned off.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumDivisors = 10;

    internal static Half Divide(Half x, int k) => k switch
    {
        0 => x / (Half)3.0f,
        1 => x / (Half)7.0f,
        2 => x / (Half)0.3f,
        3 => x / (Half)(-1.7f),
        4 => x / (Half)1000.3f,
        5 => x / (Half)0.25f,
        6 => x / (Half)65504.0f,
        7 => x / BitConverter.UInt16BitsToHalf(0x0010),
        8 => x / BitConverter.UInt16BitsToHalf(0x0005),
        _ => x / (Half)11.0f,
    };

    public static void HalfDivisionByConstantsKernel(
        Index1D index,
        ArrayView1D<ushort, Stride1D.Dense> output)
    {
        var x = BitConverter.UInt16BitsToHalf((ushort)(int)index);
        int offset = index * NumDivisors;
        output[offset] = BitConverter.HalfToUInt16Bits(Divide(x, 0));
        output[offset + 1] = BitConverter.HalfToUInt16Bits(Divide(x, 1));
        output[offset + 2] = BitConverter.HalfToUInt16Bits(Divide(x, 2));
        output[offset + 3] = BitConverter.HalfToUInt16Bits(Divide(x, 3));
        output[offset + 4] = BitConverter.HalfToUInt16Bits(Divide(x, 4));
        output[offset + 5] = BitConverter.HalfToUInt16Bits(Divide(x, 5));
        output[offset + 6] = BitConverter.HalfToUInt16Bits(Divide(x, 6));
        output[offset + 7] = BitConverter.HalfToUInt16Bits(Divide(x, 7));
        output[offset + 8] = BitConverter.HalfToUInt16Bits(Divide(x, 8));
        output[offset + 9] = BitConverter.HalfToUInt16Bits(Divide(x, 9));
    }
}

static class Program
{
    const int NumValues = 1 << 16;

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var output = stream.Allocate1D<ushort>(NumValues * Kernels.NumDivisors);
        stream.Launch((Index1D)NumValues, index => Kernels.HalfDivisionByConstantsKernel(index, output.View));
        stream.Synchronize();

        var quotients = output.GetAsArray1D();
        for (int k = 0; k < Kernels.NumDivisors; ++k)
        {
            int mismatches = 0;
            for (int i = 0; i < NumValues; ++i)
            {
                var expected = Kernels.Divide(BitConverter.UInt16BitsToHalf((ushort)i), k);
                var actual = BitConverter.UInt16BitsToHalf(quotients[i * Kernels.NumDivisors + k]);
                bool equal = Half.IsNaN(expected)
                    ? Half.IsNaN(actual)
                    : BitConverter.HalfToUInt16Bits(expected) == BitConverter.HalfToUInt16Bits(actual);
                if (!equal)
                    ++mismatches;
            }
            Console.WriteLine($"{k}: {mismatches}");
        }
    }
}
