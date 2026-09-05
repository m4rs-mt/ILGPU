// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MergedClosureDelegate.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: MergedClosureDelegate
// Delegates selected by a condition are invoked and passed to a helper, where
// the closure of one target is only created in one branch (so the selecting
// handle is passed as closure), and where two targets share one closure.
// Computes f(i) + 100 * Apply(f, i) = 101 * f(i), k = 10.
// Expected output:
// BranchClosure, flag 0 (f = 2x):             0 202 404 606
// BranchClosure, flag 1 (f = x + k + flag):   1111 1212 1313 1414
// SharedClosure, flag 0 (f = x - k):          -1010 -909 -808 -707
// SharedClosure, flag 1 (f = x + k):          1010 1111 1212 1313

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Apply(Func<int, int> f, int x) => f(x);

    public static void BranchClosureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int flag, int k)
    {
        Func<int, int> f;
        if (flag > 0)
        {
            int offset = k + flag;
            f = x => x + offset;
        }
        else
        {
            f = x => x * 2;
        }
        data[index] = f(index.X) + 100 * Apply(f, index.X);
    }

    public static void SharedClosureKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int flag, int k)
    {
        Func<int, int> add = x => x + k;
        Func<int, int> sub = x => x - k;
        Func<int, int> f = flag > 0 ? add : sub;
        data[index] = f(index.X) + 100 * Apply(f, index.X);
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
        stream.Launch((Index1D)4, index => Kernels.BranchClosureKernel(index, view0, 0, 10));
        stream.Launch((Index1D)4, index => Kernels.BranchClosureKernel(index, view1, 1, 10));
        stream.Launch((Index1D)4, index => Kernels.SharedClosureKernel(index, view2, 0, 10));
        stream.Launch((Index1D)4, index => Kernels.SharedClosureKernel(index, view3, 1, 10));
        stream.Synchronize();

        foreach (var buffer in buffers)
        {
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
            buffer.Dispose();
        }
    }
}
