// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineDelegateArgument.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: InlineDelegateArgument
// Launch lambdas pass lambda expressions to a kernel method taking a Func.
// The launches are lifted as inline kernels, so the kernel method is bound to
// each lambda, including one capturing a variable of the launching method.
// Expected output: 9 8 7 6 (negated, then offset = 10 added)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void UnaryOpKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, Func<int, int> op) =>
        data[index] = op(data[index]);
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(new[] { 1, 2, 3, 4 });
        var view = buffer.View;
        int offset = 10;

        stream.Launch((Index1D)4, index => Kernels.UnaryOpKernel(index, view, x => -x));
        stream.Launch((Index1D)4, index => Kernels.UnaryOpKernel(index, view, x => x + offset));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
