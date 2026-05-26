// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PreciseDoubleConstant.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PreciseDoubleConstant
// Kernel multiplies by Math.PI, whose exact double value needs 16 significant
// digits, so it only survives round-trip emission.
// Expected output: 3.141592653589793 (four times)

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PreciseDoubleConstantKernel(
        Index1D index, ArrayView1D<double, Stride1D.Dense> data)
    {
        data[index] = data[index] * Math.PI;
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

        using var buffer = stream.Allocate1D<double>(4);
        buffer.View.CopyFromCPU(stream, new double[] { 1.0, 1.0, 1.0, 1.0 });
        stream.Launch((Index1D)4, index => Kernels.PreciseDoubleConstantKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v.ToString("R", CultureInfo.InvariantCulture));
    }
}
