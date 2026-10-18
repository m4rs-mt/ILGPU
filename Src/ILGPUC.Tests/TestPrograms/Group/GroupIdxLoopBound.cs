// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupIdxLoopBound.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: GroupIdxLoopBound
// Loops bounded by the group index, which GPU backends read from unsigned
// built-ins. The first loop has two exit conditions; the second one starts
// at a negative value, so it runs only if the comparison is signed.
// With 4 threads: first[t] = sum of (i + 1) for i in 0..t,
// second[t] = sum of (i + 3) for i in -2..t
// Expected output: 1 3 6 10 6 10 15 21

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupIdxLoopBoundKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int groupIdx = Group.Index;
        int sum = 0;
        for (int i = 0; i <= groupIdx && i < 32; ++i)
            sum += i + 1;
        data[index] = sum;
    }

    public static void GroupIdxNegativeStartKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int sum = 0;
        for (int i = -2; i <= Group.Index; ++i)
            sum += i + 3;
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

        using var first = stream.Allocate1D<int>(4);
        using var second = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.GroupIdxLoopBoundKernel(index, first.View));
        stream.Launch((Index1D)4, index => Kernels.GroupIdxNegativeStartKernel(index, second.View));
        stream.Synchronize();

        foreach (var v in first.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in second.GetAsArray1D())
            Console.WriteLine(v);
    }
}
