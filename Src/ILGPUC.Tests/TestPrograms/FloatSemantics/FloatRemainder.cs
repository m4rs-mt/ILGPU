// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloatRemainder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: FloatRemainder
// The % operator on floats truncates the quotient like C's fmod; C-family
// targets have no % operator for floating-point operands.
// Expected output: 1.5 -1.5 2 1.25

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void FloatRemainderKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> a,
        ArrayView1D<float, Stride1D.Dense> b,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        output[index] = a[index] % b[index];
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

        using var a = stream.Allocate1D(new float[] { 5.5f, -5.5f, 7.0f, 10.25f });
        using var b = stream.Allocate1D(new float[] { 2.0f, 2.0f, 2.5f, -3.0f });
        using var output = stream.Allocate1D<float>(4);
        stream.Launch((Index1D)4, index => Kernels.FloatRemainderKernel(index, a.View, b.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
