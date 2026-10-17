// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodGroupDelegateArgument.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: MethodGroupDelegateArgument
// A helper takes a Func parameter and is called with a static method group.
// Expected output: 1 2 5 10

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Apply(Func<int, int> f, int x) => f(x);

    static int SquarePlusOne(int x) => x * x + 1;

    public static void MethodGroupDelegateArgumentKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Apply(SquarePlusOne, index.X);
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
        stream.Launch((Index1D)4, index => Kernels.MethodGroupDelegateArgumentKernel(index, buffer.View));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
