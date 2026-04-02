// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivLong.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: BinaryIntOp Div<long>
// Kernel divides two arrays element-wise.
// Input a: [10, 20, 30, 40], b: [2, 4, 5, 8]
// Expected output: 5 5 6 5

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DivKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>
    {
        result[index] = a[index] / b[index];
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

        long[] aData = [10, 20, 30, 40];
        long[] bData = [2, 4, 5, 8];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<long>(4);
        stream.Launch((Index1D)4, index => UNKNOWN(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
