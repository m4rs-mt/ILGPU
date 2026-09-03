// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalFunctionCapture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: LocalFunctionCapture
// A local function inside the kernel captures a kernel parameter and a local.
// The C# compiler passes both through a closure struct with auto layout.
// Expected output: 5 15 25 35 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LocalFunctionCaptureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int offset)
    {
        int scale = 10;
        int Apply(int value) => value * scale + offset;
        data[index] = Apply(index.X);
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
        stream.Launch((Index1D)4, index => Kernels.LocalFunctionCaptureKernel(index, buffer.View, 5));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
