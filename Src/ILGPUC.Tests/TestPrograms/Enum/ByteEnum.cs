// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ByteEnum.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: ByteEnum
// Kernel casts byte-backed enum to int.
// Expected output: 1 1 1 1

using System;
using ILGPU;
using ILGPU.Runtime;

enum ByteEnum : byte { A = 1, B = 2 }

static class Kernels
{
    public static void ByteEnumKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        ByteEnum e = ByteEnum.A;
        data[index] = (int)(byte)e;
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
        stream.Launch((Index1D)4, index => Kernels.ByteEnumKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
