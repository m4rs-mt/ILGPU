// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ChainCall.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ChainCall
// Kernel chains helper calls: Add(2,3)=5, Multiply(5,4)=20, Add(20,2)=22.
// Expected output: 22 22 22 22

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Add(int a, int b) => a + b;
    static int Multiply(int a, int b) => a * b;

    public static void ChainCallKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b, int c)
    {
        int step1 = Add(a, b);
        int step2 = Multiply(step1, c);
        int step3 = Add(step2, a);
        data[index] = step3;
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
        stream.Launch((Index1D)4, index => Kernels.ChainCallKernel(index, buffer.View, 2, 3, 4));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
