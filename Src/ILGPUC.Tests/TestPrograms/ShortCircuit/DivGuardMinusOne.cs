// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivGuardMinusOne.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: DivGuardMinusOne
// Signed divisions and remainders by -1 in the arm of a conditional expression
// that the threads holding the minimum value of the type do not take: the
// minimum value divided by -1 overflows, which throws on the CPU backend. The
// divisor of the last kernel is -1 only after inlining. Input: MinValue,
// MinValue, 6, 8; threads 2 and 3 take the arm.
// Expected output (one line per kernel):
// -2147483648 -2147483648 -6 -8
// -2147483648 -2147483648 0 0
// -9223372036854775808 -9223372036854775808 -6 -8
// -9223372036854775808 -9223372036854775808 0 0
// -2147483648 -2147483648 -6 -8

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DivMinusOneKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        data[index] = index.X >= 2 ? x / -1 : x;
    }

    public static void RemMinusOneKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        data[index] = index.X >= 2 ? x % -1 : x;
    }

    public static void DivMinusOneLongKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        long x = data[index];
        data[index] = index.X >= 2 ? x / -1L : x;
    }

    public static void RemMinusOneLongKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        long x = data[index];
        data[index] = index.X >= 2 ? x % -1L : x;
    }

    private static int Divide(int x, int y) => x / y;

    public static void DivInlinedMinusOneKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        data[index] = index.X >= 2 ? Divide(x, -1) : x;
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

        int[] ints = [int.MinValue, int.MinValue, 6, 8];
        long[] longs = [long.MinValue, long.MinValue, 6, 8];
        using var divInts = stream.Allocate1D(ints);
        using var remInts = stream.Allocate1D(ints);
        using var divLongs = stream.Allocate1D(longs);
        using var remLongs = stream.Allocate1D(longs);
        using var inlinedInts = stream.Allocate1D(ints);

        stream.Launch((Index1D)4, index =>
            Kernels.DivMinusOneKernel(index, divInts.View));
        stream.Launch((Index1D)4, index =>
            Kernels.RemMinusOneKernel(index, remInts.View));
        stream.Launch((Index1D)4, index =>
            Kernels.DivMinusOneLongKernel(index, divLongs.View));
        stream.Launch((Index1D)4, index =>
            Kernels.RemMinusOneLongKernel(index, remLongs.View));
        stream.Launch((Index1D)4, index =>
            Kernels.DivInlinedMinusOneKernel(index, inlinedInts.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", divInts.GetAsArray1D()));
        Console.WriteLine(string.Join(" ", remInts.GetAsArray1D()));
        Console.WriteLine(string.Join(" ", divLongs.GetAsArray1D()));
        Console.WriteLine(string.Join(" ", remLongs.GetAsArray1D()));
        Console.WriteLine(string.Join(" ", inlinedInts.GetAsArray1D()));
    }
}
