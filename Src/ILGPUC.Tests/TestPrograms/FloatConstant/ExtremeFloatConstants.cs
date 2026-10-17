// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ExtremeFloatConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ExtremeFloatConstants
// Float constants whose round-trip form uses an exponent, at both ends of the range.
// Expected output: 3.4028235E+38 1E+20 1E-20 -0

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ExtremeFloatConstantsKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> data)
    {
        float x = data[index];
        data[index] = index.X switch
        {
            0 => x * 3.4028235E+38f,
            1 => x * 1E+20f,
            2 => x * 1E-20f,
            _ => x * -0.0f,
        };
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
        buffer.View.CopyFromCPU(stream, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
        stream.Launch((Index1D)4, index => Kernels.ExtremeFloatConstantsKernel(index, buffer.View));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v.ToString("R", CultureInfo.InvariantCulture));
    }
}
