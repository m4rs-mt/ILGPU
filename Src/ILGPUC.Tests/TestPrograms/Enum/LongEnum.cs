// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LongEnum.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LongEnum
// Kernel casts long-backed enum to int.
// Expected output: 2000 2000 2000 2000

using System;
using ILGPU;
using ILGPU.Runtime;

enum LongEnum : long { Big = 1000L, Bigger = 2000L }

static class Kernels
{
    public static void LongEnumKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        LongEnum e = LongEnum.Bigger;
        data[index] = (int)(long)e;
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
        stream.Launch((Index1D)4, index => Kernels.LongEnumKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
