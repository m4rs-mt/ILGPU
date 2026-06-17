// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedRefReturn.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SharedRefReturn
// A helper method returns a reference into shared memory that is written to.
// With 4 threads: shared[t + 1] = t + 100
// Expected output: 100 101 102 103

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static ref int Next(ref int value) => ref Unsafe.Add(ref value, 1);

    public static void SharedRefReturnKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(33);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx + 1] = 0;
            Next(ref shared[groupIdx]) = index.X + 100;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx + 1];
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
        stream.Launch((Index1D)4, index => Kernels.SharedRefReturnKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
