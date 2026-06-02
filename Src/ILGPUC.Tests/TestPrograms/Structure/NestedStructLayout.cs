// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedStructLayout.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NestedStructLayout
// A nested struct keeps its own alignment inside the outer struct, so the
// device layout of the flattened fields must match the host layout.
// Expected output: 7 1 2 3 9 7 2 4 6 9

using System;
using ILGPU;
using ILGPU.Runtime;

struct Mixed
{
    public int A;
    public int B;
    public long C;
}

struct NestedL
{
    public byte Tag;
    public Mixed Inner;
    public short S;
}

static class Kernels
{
    public static void NestedStructLayoutKernel(
        Index1D index, ArrayView1D<NestedL, Stride1D.Dense> input, ArrayView1D<long, Stride1D.Dense> output)
    {
        var value = input[index];
        output[index * 5 + 0] = value.Tag;
        output[index * 5 + 1] = value.Inner.A;
        output[index * 5 + 2] = value.Inner.B;
        output[index * 5 + 3] = value.Inner.C;
        output[index * 5 + 4] = value.S;
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

        var values = new NestedL[2];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = new NestedL
            {
                Tag = 7,
                Inner = new Mixed { A = i + 1, B = 2 * (i + 1), C = 3 * (i + 1) },
                S = 9,
            };
        }
        using var input = stream.Allocate1D<NestedL>(2);
        input.View.CopyFromCPU(stream, values);
        using var output = stream.Allocate1D<long>(10);
        stream.Launch((Index1D)2, index => Kernels.NestedStructLayoutKernel(index, input.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
