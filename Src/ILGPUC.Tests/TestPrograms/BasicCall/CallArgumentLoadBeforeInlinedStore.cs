// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CallArgumentLoadBeforeInlinedStore.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: CallArgumentLoadBeforeInlinedStore
// Reads an element, calls an inlined helper that overwrites it, then passes the value
// read to a second inlined helper, which receives the original element.
// Expected output: 1 2 3 4

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Bump(ArrayView1D<int, Stride1D.Dense> data, Index1D i) =>
        data[i] = data[i] + 100;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Id(int x) => x + 1;

    public static void CallArgumentLoadBeforeInlinedStoreKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int a = data[index];
        Bump(data, index);
        output[index] = Id(a);
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

        using var data = stream.Allocate1D(new int[] { 0, 1, 2, 3 });
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.CallArgumentLoadBeforeInlinedStoreKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
