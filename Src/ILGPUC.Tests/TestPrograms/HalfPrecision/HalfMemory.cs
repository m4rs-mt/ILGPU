// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HalfMemory
// Half values round-trip through views, structures and uniform kernel
// parameters, and the BitConverter and Interop bit casts reinterpret their
// bits. Each lane stores scale * data[i] + offset.Bias into the view and
// prints the stored bits, the bits of a reinterpreted value and the
// structure fields.

using System;
using ILGPU;
using ILGPU.Runtime;

struct HalfPair
{
    public Half Bias;
    public Half Scale;
    public int Tag;
}

static class Kernels
{
    public static void HalfMemoryKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> data,
        Half scale,
        HalfPair pair,
        ArrayView1D<HalfPair, Stride1D.Dense> pairs,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        var value = scale * data[index] * pair.Scale + pair.Bias;
        data[index] = value;

        var stored = new HalfPair
        {
            Bias = value,
            Scale = BitConverter.UInt16BitsToHalf((ushort)(0x3C00 + index.X)),
            Tag = index,
        };
        pairs[index] = stored;

        output[index * 4] = BitConverter.HalfToUInt16Bits(data[index]);
        output[index * 4 + 1] = BitConverter.HalfToInt16Bits(-pairs[index].Scale);
        output[index * 4 + 2] = Interop.FloatAsInt(pairs[index].Bias);
        output[index * 4 + 3] = BitConverter.HalfToUInt16Bits(
            Interop.IntAsFloat((ushort)(0x4000 | index.X))) + pairs[index].Tag;
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

        var input = new Half[12];
        for (int i = 0; i < input.Length; ++i)
            input[i] = (Half)(i - 4.5f);

        using var data = stream.Allocate1D(input);
        using var pairs = stream.Allocate1D<HalfPair>(input.Length);
        using var output = stream.Allocate1D<long>(4 * input.Length);
        var pair = new HalfPair { Bias = (Half)0.125f, Scale = (Half)(-2.0f), Tag = 7 };
        var scale = (Half)1.5f;
        stream.Launch((Index1D)input.Length, index => Kernels.HalfMemoryKernel(index, data.View, scale, pair, pairs.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in data.GetAsArray1D())
            Console.WriteLine(BitConverter.HalfToUInt16Bits(v));
    }
}
