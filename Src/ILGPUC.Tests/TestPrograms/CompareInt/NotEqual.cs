// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NotEqual.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: CompareInt NotEqual<int>
// Kernel compares a != b element-wise.
// a: [1, 2, 3, 4], b: [1, 5, 3, 8] → result: [0, 1, 0, 1]
// Expected output: 0 1 0 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NotEqualKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> a,
        ArrayView1D<int, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = (a[index] != b[index]) ? 1 : 0;
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

        int[] aData = [1, 2, 3, 4];
        int[] bData = [1, 5, 3, 8];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.NotEqualKernel(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
