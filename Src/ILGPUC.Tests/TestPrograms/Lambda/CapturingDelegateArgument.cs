// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CapturingDelegateArgument.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: CapturingDelegateArgument
// A helper takes a Func parameter and is called with a lambda capturing a
// kernel parameter, whose closure is passed on to the helper.
// Expected output: 10 11 12 13 (k = 10)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Apply(Func<int, int> f, int x) => f(x);

    public static void CapturingDelegateArgumentKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int k)
    {
        data[index] = Apply(x => x + k, index.X);
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
        stream.Launch((Index1D)4, index => Kernels.CapturingDelegateArgumentKernel(index, buffer.View, 10));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
