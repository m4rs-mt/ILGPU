// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedScopeCapture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NestedScopeCapture
// Lambda captures locals of two nested scopes (offset=index*10 and scale=2 of the
// kernel, bias=3 of the inner block) and computes x*scale+offset+bias. The inner
// closure class refers to the outer one through a field.
// Expected output: 3 15 27 39 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NestedScopeCaptureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int offset = index * 10;
        int scale = 2;
        if (index >= 0)
        {
            int bias = 3;
            Func<int, int> f = x => x * scale + offset + bias;
            data[index] = f(index);
        }
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.NestedScopeCaptureKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
