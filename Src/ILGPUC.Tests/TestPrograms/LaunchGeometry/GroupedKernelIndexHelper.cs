// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupedKernelIndexHelper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupedKernelIndexHelper
// Explicitly grouped launch of a kernel that passes its KernelIndex to a
// helper method that is not inlined. The helper encodes the grid and group
// index of every thread.
// Expected output: 0 1 2 3 1000 1001 1002 1003 2000 2001 2002 2003

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Encode(KernelIndex index) =>
        (int)index.GridIndex * 1000 + index.GroupIndex;

    public static void KernelIndexHelperKernel(
        KernelIndex index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[(int)index.GridIndex * Group.Dimension + index.GroupIndex] =
            Encode(index);
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
        const int GroupSize = 4;
        using var output = stream.Allocate1D<int>(3 * GroupSize);
        stream.Launch(new KernelConfig(3, GroupSize), index =>
            Kernels.KernelIndexHelperKernel(index, output.View));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
    }
}
