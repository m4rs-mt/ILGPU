// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SelectedDelegate.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SelectedDelegate
// The delegate itself is chosen by a condition, so the Invoke site sees a phi
// of two different lambdas and must dispatch on the condition.
// Expected output: 0 2 4 6 (flag = 0 selects dbl), then 1 2 3 4 (flag = 1
// selects inc)

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SelectedDelegateKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int flag)
    {
        Func<int, int> inc = x => x + 1;
        Func<int, int> dbl = x => x * 2;
        Func<int, int> f = flag > 0 ? inc : dbl;
        data[index] = f(index.X);
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

        using var buffer0 = stream.Allocate1D<int>(4);
        using var buffer1 = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.SelectedDelegateKernel(index, buffer0.View, 0));
        stream.Launch((Index1D)4, index => Kernels.SelectedDelegateKernel(index, buffer1.View, 1));
        stream.Synchronize();

        foreach (var v in buffer0.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in buffer1.GetAsArray1D())
            Console.WriteLine(v);
    }
}
