// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalRefHelper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: LocalRefHelper
// A helper method increments a local variable through a reference.
// With 4 threads: value = 10 * t + 1
// Expected output: 1 11 21 31

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Increment(ref int value) => value += 1;

    public static void LocalRefHelperKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X * 10;
        Increment(ref value);
        data[index] = value;
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
        stream.Launch((Index1D)4, index => Kernels.LocalRefHelperKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
