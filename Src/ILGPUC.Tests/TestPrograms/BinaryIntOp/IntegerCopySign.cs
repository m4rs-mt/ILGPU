// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IntegerCopySign.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: IntegerCopySign
// sbyte, short, int and long CopySign, called directly and through generic
// math, on positive and negative values and signs, zeros and the minimum and
// maximum values whose magnitudes are representable, compared with the
// results of .NET. The last line holds the results of the minimum values with
// a uniform positive sign, whose negation wraps around in kernels.
// Expected output: mismatches 0, then -128 -32768 -2147483648 -9223372036854775808

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    internal const int NumResults = 8;

    static T CopySignOf<T>(T value, T sign) where T : INumber<T> =>
        T.CopySign(value, sign);

    public static void IntegerCopySignKernel(
        Index1D index,
        ArrayView1D<sbyte, Stride1D.Dense> sbytes,
        ArrayView1D<short, Stride1D.Dense> shorts,
        ArrayView1D<int, Stride1D.Dense> ints,
        ArrayView1D<long, Stride1D.Dense> longs,
        ArrayView1D<long, Stride1D.Dense> output,
        int uniformSign)
    {
        // Each input holds the value and the sign of a lane next to each other
        int input = index * 2;
        int offset = index * NumResults;
        output[offset + 0] = sbyte.CopySign(sbytes[input], sbytes[input + 1]);
        output[offset + 1] = CopySignOf(sbytes[input], sbytes[input + 1]);
        output[offset + 2] = short.CopySign(shorts[input], shorts[input + 1]);
        output[offset + 3] = CopySignOf(shorts[input], shorts[input + 1]);
        output[offset + 4] = int.CopySign(ints[input], ints[input + 1]);
        output[offset + 5] = CopySignOf(ints[input], ints[input + 1]);
        output[offset + 6] = long.CopySign(longs[input], longs[input + 1]);
        output[offset + 7] = CopySignOf(longs[input], longs[input + 1]);
        if (index == 0)
        {
            int last = (int)ints.Length / 2 * NumResults;
            output[last + 0] = sbyte.CopySign(sbyte.MinValue, (sbyte)uniformSign);
            output[last + 1] = short.CopySign(short.MinValue, (short)uniformSign);
            output[last + 2] = int.CopySign(int.MinValue, uniformSign);
            output[last + 3] = long.CopySign(long.MinValue, uniformSign);
        }
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

        // Pairs of values and signs
        sbyte[] sbytes =
        [
            5, 3, 5, -3, -5, 3, -5, -3, 0, 1, 0, -1, 7, 0,
            127, -1, 127, -128, -127, 0, -128, -1, -128, -128,
        ];
        short[] shorts =
        [
            5, 3, 5, -3, -5, 3, -5, -3, 0, 1, 0, -1, 7, 0,
            32767, -1, 32767, -32768, -32767, 0, -32768, -1, -32768, -32768,
        ];
        int[] ints =
        [
            5, 3, 5, -3, -5, 3, -5, -3, 0, 1, 0, -1, 7, 0,
            int.MaxValue, -1, int.MaxValue, int.MinValue, -int.MaxValue, 0,
            int.MinValue, -1, int.MinValue, int.MinValue,
        ];
        long[] longs =
        [
            5, 3, 5, -3, -5, 3, -5, -3, 0, 1, 0, -1, 7, 0,
            long.MaxValue, -1, long.MaxValue, long.MinValue, -long.MaxValue, 0,
            long.MinValue, -1, long.MinValue, long.MinValue,
        ];
        int numLanes = ints.Length / 2;

        using var a = stream.Allocate1D(sbytes);
        using var b = stream.Allocate1D(shorts);
        using var c = stream.Allocate1D(ints);
        using var d = stream.Allocate1D(longs);
        using var output = stream.Allocate1D<long>(numLanes * Kernels.NumResults + 4);
        stream.Launch(
            (Index1D)numLanes,
            index => Kernels.IntegerCopySignKernel(
                index, a.View, b.View, c.View, d.View, output.View, 1));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        int mismatches = 0;
        for (int i = 0; i < numLanes; ++i)
        {
            int k = i * 2;
            long[] expected =
            [
                sbyte.CopySign(sbytes[k], sbytes[k + 1]),
                sbyte.CopySign(sbytes[k], sbytes[k + 1]),
                short.CopySign(shorts[k], shorts[k + 1]),
                short.CopySign(shorts[k], shorts[k + 1]),
                int.CopySign(ints[k], ints[k + 1]),
                int.CopySign(ints[k], ints[k + 1]),
                long.CopySign(longs[k], longs[k + 1]),
                long.CopySign(longs[k], longs[k + 1]),
            ];
            for (int j = 0; j < Kernels.NumResults; ++j)
            {
                if (values[i * Kernels.NumResults + j] != expected[j])
                    ++mismatches;
            }
        }
        Console.WriteLine($"mismatches {mismatches}");

        int last = numLanes * Kernels.NumResults;
        Console.WriteLine(
            $"{values[last]} {values[last + 1]} {values[last + 2]} {values[last + 3]}");
    }
}
