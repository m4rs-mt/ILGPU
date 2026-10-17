// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PreciseFloatConstant.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PreciseFloatConstant
// Kernel multiplies by a float constant whose exact value needs more than six
// decimal places, so it only survives round-trip emission.
// Expected output: 1.2345678 1.2345678 1.2345678 1.2345678

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PreciseFloatConstantKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> data)
    {
        data[index] = data[index] * 1.2345678f;
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
        stream.Launch((Index1D)4, index => Kernels.PreciseFloatConstantKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v.ToString("R", CultureInfo.InvariantCulture));
    }
}
