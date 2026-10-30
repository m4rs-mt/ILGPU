// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructPassByValueFields.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: StructPassByValueFields
// A struct parameter with signed byte and unsigned fields is passed by value.
// Expected output: per thread the fields, the sum of the fields and the index.

using System;
using ILGPU;
using ILGPU.Runtime;

struct Fields
{
    public sbyte A;
    public ushort B;
    public uint C;
    public ulong D;
    public short E;
}

static class Kernels
{
    public static void StructPassByValueFieldsKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> data, Fields fields)
    {
        data[index] = index + fields.A + fields.B + (long)fields.C + (long)fields.D + fields.E;
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

        using var buffer = stream.Allocate1D<long>(4);
        var fields = new Fields { A = -7, B = 60000, C = 3000000000, D = 5000000000, E = -300 };
        stream.Launch((Index1D)4, index => Kernels.StructPassByValueFieldsKernel(index, buffer.View, fields));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
