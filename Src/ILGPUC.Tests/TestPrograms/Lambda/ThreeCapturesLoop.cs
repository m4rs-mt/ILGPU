// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ThreeCapturesLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ThreeCapturesLoop
// A closure with three captures, invoked inside a loop (reads outside the entry
// block, in a loop body and across the back edge).
// a = 1, b = 2, c = 3: f(j) = j * a + b - c = j - 1; sum over j < n with n = index + 1.
// Expected output: -1 -1 0 2

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ThreeCapturesLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b, int c)
    {
        Func<int, int> f = j => j * a + b - c;
        int sum = 0;
        for (int j = 0; j <= index.X; ++j)
            sum += f(j);
        data[index] = sum;
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
        stream.Launch((Index1D)4, index => Kernels.ThreeCapturesLoopKernel(index, buffer.View, 1, 2, 3));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
