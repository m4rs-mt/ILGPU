// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedVariable.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SharedVariable
// Kernel allocates shared memory, writes index*2, barriers, reads back.
// With 4 threads: shared = [0,2,4,6], data = [0,2,4,6]
// Expected output: 0 2 4 6

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SharedMemoryVariableKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;

        if (groupIdx < 32)
            shared[groupIdx] = groupIdx * 2;
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx];
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
        stream.Launch((Index1D)4, index => Kernels.SharedMemoryVariableKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
