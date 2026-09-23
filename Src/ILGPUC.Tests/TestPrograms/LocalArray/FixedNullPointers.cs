// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FixedNullPointers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalArray FixedNullPointers
// A fixed statement pins the unassigned (null) array of a default structure,
// which is the same for all threads, and the local array of each thread. Two
// pointer locals start as null and take both pinned pointers if the output has
// more than two elements, so that the null pointer is an argument of two
// pointer phis of the same block: one uniform and one per thread. Thread i
// writes -10 + i, as the pointer of the null array is null.
// Expected output: -10 -9 -8 -7

using System;
using ILGPU;
using ILGPU.Runtime;

struct Holder
{
    public int[] Values;
}

static class Kernels
{
    public static unsafe void FixedNullPointersKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        Holder holder = default;
        int[] values = new int[1];
        values[0] = index.X;
        fixed (int* a = holder.Values, b = values)
        {
            int* p = null;
            int* q = null;
            if (output.IntLength > 2)
            {
                p = a;
                q = b;
            }
            output[index] = (p == null ? -1 : *p) * 10 + *q;
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

        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.FixedNullPointersKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
