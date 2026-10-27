// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecordStructInstanceCall.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: RecordStructInstanceCall
// Instance methods on a readonly record struct held in a kernel local: the receiver is
// passed by reference from local memory.
// Expected output (one per line):
//   Pack:     16909060 33752069 50595078 67438087
//   Distance: 16909060 84281112 16909060 84281112

using System;
using ILGPU;
using ILGPU.Runtime;

readonly record struct Packed(byte A, byte R, byte G, byte B)
{
    public uint Pack() => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;

    public int Distance(in Packed other) =>
        A - other.A + (R - other.R) + (G - other.G) + (B - other.B);
}

static class Kernels
{
    public static void PackKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<uint, Stride1D.Dense> output)
    {
        int v = input[index];
        var color = new Packed((byte)v, (byte)(v + 1), (byte)(v + 2), (byte)(v + 3));
        output[index] = color.Pack();
    }

    public static void DistanceKernel(
        Index1D index,
        ArrayView1D<uint, Stride1D.Dense> output)
    {
        var a = new Packed(1, 2, 3, 4);
        var b = new Packed(5, 6, 7, 8);
        var c = index % 2 == 0 ? a : b;
        output[index] = c.Pack() + (uint)c.Distance(in a);
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

        using var input = stream.Allocate1D(new[] { 1, 2, 3, 4 });
        using var pack = stream.Allocate1D<uint>(4);
        using var distance = stream.Allocate1D<uint>(4);

        stream.Launch((Index1D)4, index =>
            Kernels.PackKernel(index, input.View, pack.View));
        stream.Launch((Index1D)4, index =>
            Kernels.DistanceKernel(index, distance.View));
        stream.Synchronize();

        foreach (var v in pack.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in distance.GetAsArray1D())
            Console.WriteLine(v);
    }
}
