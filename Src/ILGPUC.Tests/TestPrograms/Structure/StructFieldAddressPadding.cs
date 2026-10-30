// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructFieldAddressPadding.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: StructFieldAddressPadding
// In-place updates of struct fields in a buffer take the field address, which
// includes the padding that aligns the fields in front of it.
// Expected output: per element "A B C D" after the update.

using System;
using ILGPU;
using ILGPU.Runtime;

struct Padded
{
    public byte A;
    public long B;
    public short C;
    public int D;
}

static class Kernels
{
    public static void StructFieldAddressPaddingKernel(
        Index1D index, ArrayView1D<Padded, Stride1D.Dense> data)
    {
        data[index].A += 1;
        data[index].B *= 3;
        data[index].C -= 2;
        data[index].D += 100;
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

        var input = new Padded[4];
        for (int i = 0; i < input.Length; i++)
            input[i] = new Padded { A = (byte)(10 + i), B = 1000 + i, C = (short)(20 + i), D = 30 + i };
        using var data = stream.Allocate1D<Padded>(4);
        data.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index => Kernels.StructFieldAddressPaddingKernel(index, data.View));
        stream.Synchronize();

        foreach (var v in data.GetAsArray1D())
            Console.WriteLine($"{v.A} {v.B} {v.C} {v.D}");
    }
}
