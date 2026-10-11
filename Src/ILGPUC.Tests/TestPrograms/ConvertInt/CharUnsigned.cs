// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CharUnsigned.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: CharUnsigned
// Kernel reads chars from a buffer and a char parameter, including values from
// 0x8000, and widens, shifts, compares, divides, adds with wrap-around and
// converts them to float as unsigned 16-bit integers.
// Expected output per lane: the char, c >> 4, c > 0x7FFF, c / 3,
// (char)(c + 0x8001) and (long)(float)c.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void CharUnsignedKernel(
        Index1D index,
        ArrayView1D<char, Stride1D.Dense> input,
        ArrayView1D<long, Stride1D.Dense> output,
        char offset)
    {
        char c = input[index];
        int o = index * 6;
        output[o] = c;
        output[o + 1] = c >> 4;
        output[o + 2] = c > '\u7fff' ? 1 : 0;
        output[o + 3] = c / 3;
        output[o + 4] = (char)(c + offset);
        output[o + 5] = (long)(float)c;
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

        char[] values = ['A', '\u7fff', '\u8000', '\uffff'];
        using var input = stream.Allocate1D(values);
        using var output = stream.Allocate1D<long>(6 * values.Length);
        stream.Launch(
            (Index1D)values.Length,
            index => Kernels.CharUnsignedKernel(
                index,
                input.View,
                output.View,
                '\u8001'));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        for (int i = 0; i < values.Length; ++i)
            Console.WriteLine(string.Join(" ", data[(i * 6)..(i * 6 + 6)]));
    }
}
