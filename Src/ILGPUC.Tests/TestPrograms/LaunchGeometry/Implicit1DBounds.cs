// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Implicit1DBounds.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: Implicit1DBounds
// Implicitly grouped 1D launch whose extent is larger than the group-size
// limit and not a multiple of it. The kernel writes through a sub-view of a
// larger buffer; the tail beyond the sub-view is a canary that must stay
// untouched, so any thread running past the extent shows up as a clobbered
// canary.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Implicit1DBoundsKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int constant)
    {
        data[index] = constant + index;
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

        const int Constant = 7;
        const int Canary = -1;
        const int Padding = 4096;
        int extent = 3 * accelerator.MaxNumThreadsPerGroup + 17;

        var initial = new int[extent + Padding];
        Array.Fill(initial, Canary);
        using var buffer = stream.Allocate1D(initial);
        var view = buffer.View.SubView(0, extent);
        stream.Launch(
            (Index1D)extent,
            index => Kernels.Implicit1DBoundsKernel(index, view, Constant));
        stream.Synchronize();

        var values = buffer.GetAsArray1D();
        int mismatches = 0;
        for (int i = 0; i < extent; ++i)
        {
            if (values[i] != Constant + i)
                ++mismatches;
        }
        int clobbered = 0;
        for (int i = extent; i < values.Length; ++i)
        {
            if (values[i] != Canary)
                ++clobbered;
        }

        Console.WriteLine($"mismatches {mismatches}");
        Console.WriteLine($"clobbered {clobbered}");
    }
}
