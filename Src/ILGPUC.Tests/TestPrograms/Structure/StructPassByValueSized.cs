// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructPassByValueSized.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: StructPassByValueSized
// A struct parameter that contains an explicitly sized struct is passed by value,
// and its fields behind the nested struct keep their host offsets.
// Expected output: per thread the sum of the fields and the index.

using System;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;

[StructLayout(LayoutKind.Sequential, Size = 24)]
struct Sized
{
    public int A;
    public short B;
}

struct WithSized
{
    public byte Tag;
    public Sized Inner;
    public short S;
}

static class Kernels
{
    public static void StructPassByValueSizedKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, WithSized value)
    {
        data[index] = index + value.Tag + value.Inner.A + value.Inner.B + value.S;
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

        using var buffer = stream.Allocate1D<int>(4);
        var value = new WithSized { Tag = 3, Inner = new Sized { A = 40, B = 500 }, S = 6000 };
        stream.Launch((Index1D)4, index => Kernels.StructPassByValueSizedKernel(index, buffer.View, value));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
