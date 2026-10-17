// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SelectedDelegateArgument.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SelectedDelegateArgument
// A delegate chosen by a condition is passed to a helper taking a Func. The
// call dispatches on the condition to the copy of the helper specialized for
// the chosen lambda, one of which captures a kernel parameter.
// Expected output: 0 2 4 6 (flag = 0 selects dbl), then 10 11 12 13 (flag = 1
// selects the lambda adding k = 10)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Apply(Func<int, int> f, int x) => f(x);

    public static void SelectedDelegateArgumentKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int flag, int k)
    {
        Func<int, int> add = x => x + k;
        Func<int, int> dbl = x => x * 2;
        data[index] = Apply(flag > 0 ? add : dbl, index.X);
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

        var buffers = new[] { stream.Allocate1D<int>(4), stream.Allocate1D<int>(4) };
        var view0 = buffers[0].View;
        var view1 = buffers[1].View;
        stream.Launch((Index1D)4, index => Kernels.SelectedDelegateArgumentKernel(index, view0, 0, 10));
        stream.Launch((Index1D)4, index => Kernels.SelectedDelegateArgumentKernel(index, view1, 1, 10));

        stream.Synchronize();

        foreach (var buffer in buffers)
        {
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
            buffer.Dispose();
        }
    }
}
