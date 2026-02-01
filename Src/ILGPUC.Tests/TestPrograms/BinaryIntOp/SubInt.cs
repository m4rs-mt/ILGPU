// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SubInt.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: BinaryIntOp Sub<int>
// Kernel subtracts two arrays element-wise.
// Input a: [10, 20, 30, 40], b: [1, 2, 3, 4]
// Expected output: 9 18 27 36

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SubKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>
    {
        result[index] = a[index] - b[index];
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

        int[] aData = [10, 20, 30, 40];
        int[] bData = [1, 2, 3, 4];
        using var a = stream.Allocate1D(aData);
        using var b = stream.Allocate1D(bData);
        using var result = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => UNKNOWN(index, a.View, b.View, result.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
