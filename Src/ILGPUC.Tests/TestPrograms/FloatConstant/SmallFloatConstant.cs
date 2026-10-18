// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SmallFloatConstant.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SmallFloatConstant
// Kernel multiplies by a float constant below the 6th decimal place, which a
// fixed-point "F6" emission would truncate to 0.
// Expected output: 1 1 1 1

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SmallFloatConstantKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> data)
    {
        data[index] = data[index] * 1e-7f;
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

        using var buffer = stream.Allocate1D<float>(4);
        buffer.View.CopyFromCPU(stream, new float[] { 1e7f, 1e7f, 1e7f, 1e7f });
        stream.Launch((Index1D)4, index => Kernels.SmallFloatConstantKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine((int)Math.Round(v));
    }
}
