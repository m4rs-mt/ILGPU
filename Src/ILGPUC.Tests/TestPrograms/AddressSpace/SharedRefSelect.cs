// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedRefSelect.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SharedRefSelect
// A reference selects one of two elements of shared memory per thread.
// With 4 threads: shared[t] = 10 * (t + 1), shared[t + 32] = t; even threads
// increment shared[t], odd threads shared[t + 32]; data[t] = shared[t] + shared[t + 32]
// Expected output: 11 22 33 44

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SharedRefSelectKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(64);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx] = 10 * (groupIdx + 1);
            shared[groupIdx + 32] = groupIdx;
        }
        Group.Barrier();

        if (groupIdx < 32)
        {
            ref int target = ref groupIdx % 2 == 0
                ? ref shared[groupIdx]
                : ref shared[groupIdx + 32];
            target += 1;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx] + shared[groupIdx + 32];
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
        stream.Launch((Index1D)4, index => Kernels.SharedRefSelectKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
