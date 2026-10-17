// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NamedArguments.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: kernel arguments passed by name in a different order, and an
// omitted optional argument, both directly and through a generic wrapper.
// Expected output: 10 20 | 13 23 (the subtracted array keeps its role)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Subtract<T>(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> minuend,
        ArrayView1D<int, Stride1D.Dense> subtrahend,
        ArrayView1D<T, Stride1D.Dense> unused,
        int offset = 0)
        where T : unmanaged
    {
        minuend[index] = minuend[index] - subtrahend[index] + offset;
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

        using var unused = stream.Allocate1D<float>(2);
        var unusedView = unused.View;
        using var a = stream.Allocate1D(new int[] { 11, 22 });
        using var b = stream.Allocate1D(new int[] { 1, 2 });
        var av = a.View;
        var bv = b.View;
        stream.Launch(
            (Index1D)2,
            i => Kernels.Subtract(i, subtrahend: bv, unused: unusedView, minuend: av));
        stream.Synchronize();
        foreach (var value in a.GetAsArray1D())
            Console.WriteLine(value);

        Run(stream, unusedView);
    }

    static void Run<T>(AcceleratorStream stream, ArrayView1D<T, Stride1D.Dense> unused)
        where T : unmanaged
    {
        using var a = stream.Allocate1D(new int[] { 11, 22 });
        using var b = stream.Allocate1D(new int[] { 1, 2 });
        var av = a.View;
        var bv = b.View;
        stream.Launch(
            (Index1D)2,
            i => Kernels.Subtract(
                i,
                offset: 3,
                subtrahend: bv,
                minuend: av,
                unused: unused));
        stream.Synchronize();
        foreach (var value in a.GetAsArray1D())
            Console.WriteLine(value);
    }
}
