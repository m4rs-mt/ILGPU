// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Index3D.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: Index3D
// Kernel uses Index3D to compute linear index and store encoded position.
// Expected output: 0 100 1 101 10000 10100 10001 10101

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Index3DKernel(
        Index3D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int width,
        int height)
    {
        int linear = index.X + index.Y * width + index.Z * width * height;
        data[linear] = index.X + index.Y * 100 + index.Z * 10000;
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

        using var buffer = stream.Allocate1D<int>(8);
        stream.Launch(new Index3D(2, 2, 2), index => Kernels.Index3DKernel(index, buffer.View, 2, 2));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
