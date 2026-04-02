// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GreaterEqualFloat.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: CompareFloat GreaterEqual<float>
// Kernel compares a >= b element-wise.
// a: [5.0, 4.0, 7.0, 8.0], b: [2.0, 4.0, 6.0, 8.0] → result: [1, 1, 1, 1]
// Expected output: 1 1 1 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GreaterEqualKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> a,
        ArrayView1D<float, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = (a[index] >= b[index]) ? 1 : 0;
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

        float[] aData = [5.0f, 4.0f, 7.0f, 8.0f];
        float[] bData = [2.0f, 4.0f, 6.0f, 8.0f];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.GreaterEqualKernel(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
