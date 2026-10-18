// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerSelect.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PointerSelect
// A pointer selects between the address of an input element and null, and the
// kernel reads the element through the pointer if it is not null.
// With 4 threads t and input = [1, 2, 3, 4]: t + 1 for even threads, -1 for odd
// threads.
// Expected output: 1 -1 3 -1

using System;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    public static void PointerSelectKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int i = index;
        fixed (int* element = &input[i])
        {
            int* p = i % 2 == 0 ? element : null;
            output[i] = p != null ? *p : -1;
        }
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

        using var input = stream.Allocate1D(new[] { 1, 2, 3, 4 });
        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.PointerSelectKernel(index, input.View, output.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
    }
}
