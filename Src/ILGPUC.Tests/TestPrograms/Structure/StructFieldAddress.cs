// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructFieldAddress.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: StructFieldAddress
// In-place updates of struct fields in a buffer take the field address,
// which must be the field's byte offset, not its index times the field size.
// Expected output: 0 1 13 1 2 23 2 3 33 3 4 43

using System;
using ILGPU;
using ILGPU.Runtime;

struct Mixed
{
    public int A;
    public int B;
    public long C;
}

static class Kernels
{
    public static void StructFieldAddressKernel(
        Index1D index, ArrayView1D<Mixed, Stride1D.Dense> data)
    {
        data[index].C += 3;
        data[index].B += 1;
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

        var input = new Mixed[4];
        for (int i = 0; i < input.Length; i++)
            input[i] = new Mixed { A = i, B = i, C = 10 * (i + 1) };
        using var data = stream.Allocate1D<Mixed>(4);
        data.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index => Kernels.StructFieldAddressKernel(index, data.View));
        stream.Synchronize();

        foreach (var v in data.GetAsArray1D())
        {
            Console.WriteLine(v.A);
            Console.WriteLine(v.B);
            Console.WriteLine(v.C);
        }
    }
}
