// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ForwardedDelegateArgument.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ForwardedDelegateArgument
// A helper forwards its Func parameter to another helper, which is specialized
// for the lambda passed to the outer helper as well.
// Expected output: 10 11 12 13 (k = 5, the lambda is applied twice)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Apply(Func<int, int> f, int x) => f(x);

    static int ApplyTwice(Func<int, int> f, int x) => Apply(f, Apply(f, x));

    public static void ForwardedDelegateArgumentKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int k)
    {
        data[index] = ApplyTwice(x => x + k, index.X);
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
        stream.Launch((Index1D)4, index => Kernels.ForwardedDelegateArgumentKernel(index, buffer.View, 5));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
