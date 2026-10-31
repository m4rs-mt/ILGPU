// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Cast.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: Cast
// Kernel casts views between element types of different sizes. The length of
// the cast view must be rescaled by sizeof(source) / sizeof(target).
// Input: ints = [1, 0, 2, 0, 3, 0, 4, 0] (8 x int), bytes = [0..15] (16 x byte)
// Expected output:
//   4          ints as long: IntLength (8 * 4 / 8)
//   32         ints as byte: IntLength (8 * 4 / 1)
//   4          bytes as int: IntLength (16 * 1 / 4)
//   8          ints as float: IntLength (same size)
//   4          ints as long: Length
//   3          ints as long: element 2
//   4          ints as long: last element
//   2          ints as byte: element 8 (low byte of ints[2])
//   117835012  bytes as int: element 1 (0x07060504)
//   4 2 4 2    per-lane views of 8 and 4 ints as long: Length

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void CastKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> ints,
        ArrayView1D<byte, Stride1D.Dense> bytes,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        if (index.X != 0)
            return;

        var intsAsLongs = ints.BaseView.Cast<long>();
        var intsAsBytes = ints.BaseView.Cast<byte>();
        var bytesAsInts = bytes.BaseView.Cast<int>();
        var intsAsFloats = ints.BaseView.Cast<float>();

        result[0] = intsAsLongs.IntLength;
        result[1] = intsAsBytes.IntLength;
        result[2] = bytesAsInts.IntLength;
        result[3] = intsAsFloats.IntLength;
        result[4] = intsAsLongs.Length;
        result[5] = intsAsLongs[2];
        result[6] = intsAsLongs[intsAsLongs.IntLength - 1];
        result[7] = intsAsBytes[8];
        result[8] = bytesAsInts[1];
    }

    public static void PerLaneCastKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> ints,
        ArrayView1D<long, Stride1D.Dense> lengths)
    {
        ArrayView<int> view = index.X % 2 == 0
            ? ints.BaseView.SubView(0, 8)
            : ints.BaseView.SubView(0, 4);
        lengths[index] = view.Cast<long>().Length;
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

        int[] intData = [1, 0, 2, 0, 3, 0, 4, 0];
        byte[] byteData = new byte[16];
        for (int i = 0; i < byteData.Length; ++i)
            byteData[i] = (byte)i;

        using var ints = stream.Allocate1D(intData);
        using var bytes = stream.Allocate1D(byteData);
        using var result = stream.Allocate1D<long>(9);
        stream.Launch(
            (Index1D)1,
            index => Kernels.CastKernel(index, ints.View, bytes.View, result.View));
        stream.Synchronize();

        var output = result.GetAsArray1D();
        foreach (var v in output)
            Console.WriteLine(v);

        using var lengths = stream.Allocate1D<long>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.PerLaneCastKernel(index, ints.View, lengths.View));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", lengths.GetAsArray1D()));
    }
}
