// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ShortEnum.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: ShortEnum
// Kernel casts short-backed enum to int.
// Expected output: 20 20 20 20

using System;
using ILGPU;
using ILGPU.Runtime;

enum ShortEnum : short { X = 10, Y = 20 }

static class Kernels
{
    public static void ShortEnumKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        ShortEnum e = ShortEnum.Y;
        data[index] = (int)(short)e;
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
        stream.Launch((Index1D)4, index => Kernels.ShortEnumKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
