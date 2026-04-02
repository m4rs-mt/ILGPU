// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedLabel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NestedLabel
// Kernel uses nested goto labels. value=0 → jumps to zero → result = 100.
// Expected output: 100 100 100 100

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NestedLabelKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int result = 0;
        if (value == 0)
            goto zero;
        if (value == 1)
            goto one;
        result = value * 3;
        goto done;
    zero:
        result = 100;
        goto done;
    one:
        result = 200;
    done:
        data[index] = result;
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
        stream.Launch((Index1D)4, index => Kernels.NestedLabelKernel(index, buffer.View, 0));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
