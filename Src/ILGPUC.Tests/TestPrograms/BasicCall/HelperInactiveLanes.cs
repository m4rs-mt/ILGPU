// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HelperInactiveLanes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: HelperInactiveLanes
// A helper that is not inlined is called in a divergent branch: its stores
// must only happen for the lanes that take the branch.
// Expected output: 100 0 0 103 0 0 106 0 0 109 0 0

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Mark(ArrayView1D<int, Stride1D.Dense> output, int index) =>
        output[index] = index + 100;

    public static void HelperInactiveLanesKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        if (index % 3 == 0)
            Mark(output, index);
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

        using var output = stream.Allocate1D<int>(12);
        output.MemSetToZero(stream);
        stream.Launch((Index1D)12, index => Kernels.HelperInactiveLanesKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
