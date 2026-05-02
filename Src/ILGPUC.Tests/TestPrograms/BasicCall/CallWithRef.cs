// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CallWithRef.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: CallWithRef
// Kernel calls helper with ref parameter: DoubleRef(ref 7) → 14.
// Expected output: 14 14 14 14

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static void DoubleRef(ref int value)
    {
        value = value * 2;
    }

    public static void CallWithRefKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int v = value;
        DoubleRef(ref v);
        data[index] = v;
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
        stream.Launch((Index1D)4, index => Kernels.CallWithRefKernel(index, buffer.View, 7));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
