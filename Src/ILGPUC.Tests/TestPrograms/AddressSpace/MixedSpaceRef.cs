// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MixedSpaceRef.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: MixedSpaceRef
// A reference selects shared memory (even threads) or global memory (odd threads).
// With 4 threads: data[t] = t (+1 if odd) + 10 * (t + 1) (+1 if even)
// Expected output: 11 22 33 44

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void MixedSpaceRefKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
            shared[groupIdx] = 10 * (groupIdx + 1);
        data[index] = index.X;
        Group.Barrier();

        if (groupIdx < 32)
        {
            ref int target = ref groupIdx % 2 == 0
                ? ref shared[groupIdx]
                : ref data[index];
            target += 1;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] += shared[groupIdx];
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.MixedSpaceRefKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
