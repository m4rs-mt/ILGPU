// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IsValid.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: IsValid
// Kernel checks if an array view is valid.
// Expected output: 1 1 1 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IsValidKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = data.IsValid ? 1 : 0;
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

        int[] inputData = [10, 20, 30, 40];
        using var data = stream.Allocate1D(inputData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.IsValidKernel(index, data.View, result.View));
        stream.Synchronize();

        var output = result.GetAsArray1D();
        foreach (var v in output)
            Console.WriteLine(v);
    }
}
