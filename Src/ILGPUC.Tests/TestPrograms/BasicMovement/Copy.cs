// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Copy.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: Copy
// Kernel copies data from source to target view.
// Input: [10,20,30,40], Expected output: 10 20 30 40

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void CopyKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> source,
        ArrayView1D<int, Stride1D.Dense> target)
    {
        target[index] = source[index];
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

        var sourceData = new int[] { 10, 20, 30, 40 };
        using var sourceBuf = stream.Allocate1D(sourceData);
        using var targetBuf = stream.Allocate1D<int>(4);

        stream.Launch((Index1D)4, index => Kernels.CopyKernel(index, sourceBuf.View, targetBuf.View));
        stream.Synchronize();

        var data = targetBuf.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
