// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BitCount.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: BitCount
// XMath.PopCount / LeadingZeroCount / TrailingZeroCount on 32- and 64-bit
// values, including 0 (whose leading and trailing zero counts are the bit
// width), -1 and the minimum values, compared with System.Numerics.BitOperations.

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void BitCountKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> ints,
        ArrayView1D<long, Stride1D.Dense> longs,
        ArrayView1D<int, Stride1D.Dense> results)
    {
        int i = ints[index];
        long l = longs[index];
        int offset = index * 6;
        results[offset + 0] = XMath.PopCount(i);
        results[offset + 1] = XMath.LeadingZeroCount(i);
        results[offset + 2] = XMath.TrailingZeroCount(i);
        results[offset + 3] = XMath.PopCount(l);
        results[offset + 4] = XMath.LeadingZeroCount(l);
        results[offset + 5] = XMath.TrailingZeroCount(l);
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

        int[] ints = [0, 1, -1, 0x00F0_0000, int.MinValue, 0x7FFF_FFFF];
        long[] longs =
        [
            0L, 1L, -1L, 0x0000_00F0_0000_0000L, long.MinValue, 0x0000_0001_0000_0000L,
        ];
        using var intBuffer = stream.Allocate1D(ints);
        using var longBuffer = stream.Allocate1D(longs);
        using var results = stream.Allocate1D<int>(ints.Length * 6);
        stream.Launch(
            (Index1D)ints.Length,
            index => Kernels.BitCountKernel(
                index, intBuffer.View, longBuffer.View, results.View));
        stream.Synchronize();

        var values = results.GetAsArray1D();
        int mismatches = 0;
        for (int k = 0; k < ints.Length; ++k)
        {
            uint u = unchecked((uint)ints[k]);
            ulong ul = unchecked((ulong)longs[k]);
            int[] expected =
            [
                BitOperations.PopCount(u),
                BitOperations.LeadingZeroCount(u),
                BitOperations.TrailingZeroCount(u),
                BitOperations.PopCount(ul),
                BitOperations.LeadingZeroCount(ul),
                BitOperations.TrailingZeroCount(ul),
            ];
            for (int j = 0; j < expected.Length; ++j)
            {
                if (values[k * 6 + j] != expected[j])
                    ++mismatches;
            }
        }
        Console.WriteLine($"mismatches {mismatches}");
    }
}
