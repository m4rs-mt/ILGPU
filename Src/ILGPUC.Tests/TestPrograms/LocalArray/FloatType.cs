// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloatType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalArray FloatType
// Kernel allocates float[4], fills {1.5,2.5,3.5,4.5}, copies to output.
// Expected output: 1.5 2.5 3.5 4.5

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void FloatArrayKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> output)
    {
        float[] local = new float[4];
        local[0] = 1.5f; local[1] = 2.5f; local[2] = 3.5f; local[3] = 4.5f;
        output[index] = local[index];
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

        using var output = stream.Allocate1D<float>(4);
        stream.Launch((Index1D)4, index => Kernels.FloatArrayKernel(index, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v.ToString(CultureInfo.InvariantCulture));
    }
}
