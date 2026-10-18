// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ExtremeDoubleConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ExtremeDoubleConstants
// Double constants whose round-trip form uses an exponent, at both ends of the range.
// Expected output: 1.7976931348623157E+308 1E-300 5E-324 1E+100

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ExtremeDoubleConstantsKernel(
        Index1D index, ArrayView1D<double, Stride1D.Dense> data)
    {
        double x = data[index];
        data[index] = index.X switch
        {
            0 => x * 1.7976931348623157E+308,
            1 => x * 1E-300,
            2 => x * 4.9406564584124654E-324,
            _ => x * 1E+100,
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

        using var buffer = stream.Allocate1D<double>(4);
        buffer.View.CopyFromCPU(stream, new double[] { 1.0, 1.0, 1.0, 1.0 });
        stream.Launch((Index1D)4, index => Kernels.ExtremeDoubleConstantsKernel(index, buffer.View));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v.ToString("R", CultureInfo.InvariantCulture));
    }
}
