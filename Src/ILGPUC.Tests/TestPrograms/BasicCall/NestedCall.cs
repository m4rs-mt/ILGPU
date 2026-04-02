// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedCall.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NestedCall
// Kernel calls nested helper methods: AddThenMultiply(2,3,4) = Multiply(Add(2,3),4) = 20.
// Expected output: 20 20 20 20

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Add(int a, int b) => a + b;
    static int Multiply(int a, int b) => a * b;
    static int AddThenMultiply(int a, int b, int c) => Multiply(Add(a, b), c);

    public static void NestedCallKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b, int c)
    {
        data[index] = AddThenMultiply(a, b, c);
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
        stream.Launch((Index1D)4, index => Kernels.NestedCallKernel(index, buffer.View, 2, 3, 4));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
