// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoopSelectedDelegate.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: LoopSelectedDelegate
// A delegate is reassigned in a loop by nested conditionals, so it selects
// between three lambdas when it is invoked or passed to a helper in the loop
// (depending on the back edge) and when it is invoked after the loop.
// Expected output: f(i) + 100 * sum for steps = 0, 1, 2 and 3:
// 1 2 3 4, 200 202 204 206, 600 601 604 609, 1400 1401 1404 1409

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Apply(Func<int, int> f, int x) => f(x);

    public static void LoopSelectedDelegateKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int steps)
    {
        Func<int, int> f = x => x + 1;
        int sum = 0;
        for (int j = 0; j < steps; j++)
        {
            sum += f(j) + Apply(f, j);
            if (j == 0)
                f = x => x * 2;
            else if (j == 1)
                f = x => x * x;
        }
        data[index] = f(index.X) + 100 * sum;
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

        var buffers = new[]
        {
            stream.Allocate1D<int>(4),
            stream.Allocate1D<int>(4),
            stream.Allocate1D<int>(4),
            stream.Allocate1D<int>(4),
        };
        var view0 = buffers[0].View;
        var view1 = buffers[1].View;
        var view2 = buffers[2].View;
        var view3 = buffers[3].View;
        stream.Launch((Index1D)4, index => Kernels.LoopSelectedDelegateKernel(index, view0, 0));
        stream.Launch((Index1D)4, index => Kernels.LoopSelectedDelegateKernel(index, view1, 1));
        stream.Launch((Index1D)4, index => Kernels.LoopSelectedDelegateKernel(index, view2, 2));
        stream.Launch((Index1D)4, index => Kernels.LoopSelectedDelegateKernel(index, view3, 3));
        stream.Synchronize();

        foreach (var buffer in buffers)
        {
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
            buffer.Dispose();
        }
    }
}
