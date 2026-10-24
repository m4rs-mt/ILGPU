// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MakeAtomicReadBack.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: MakeAtomicReadBack
// Reads the target of a custom atomic built with Atomic.MakeAtomic (a compare-and-swap
// loop on every target) after the atomic and passes the value read to a second custom
// atomic in a branch, which receives the value the first atomic stored.
// Expected output: 5 5 5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void MakeAtomicReadBackKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        Atomic.MakeAtomic(
            ref data[0],
            5,
            (current, value) => current > value ? current : value,
            (ref int target, int compare, int value) =>
                Atomic.CompareExchange(ref target, compare, value));
        int read = data[0];
        if (index == 0)
        {
            Atomic.MakeAtomic(
                ref data[1],
                read,
                (current, value) => current > value ? current : value,
                (ref int target, int compare, int value) =>
                    Atomic.CompareExchange(ref target, compare, value));
        }
        output[index] = read;
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

        using var data = stream.Allocate1D(new int[] { 2, 1 });
        using var output = stream.Allocate1D<int>(1);
        stream.Launch((Index1D)1, index => Kernels.MakeAtomicReadBackKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in data.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
