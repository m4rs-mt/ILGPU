// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RefSelect.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalArray RefSelect
// A reference selects one of two elements of a local array per thread, which
// failed to compile at O0 as the element addresses flow into a phi. Thread i
// writes i + 1 through the reference:
// - RefSelectKernel: to local[1] for even and local[2] for odd threads, then
//   writes local[1] * 10 + local[2];
// - RefSelectIndexKernel: to local[0] for even threads and local[i & 3] for odd
//   threads, then writes local[0] * 10 + local[i & 3];
// - RefFieldSelectKernel: to field A of local[0] for even and field B of
//   local[1] for odd threads of a local array of structures, then writes
//   local[0].A * 10 + local[1].B.
// Expected output: 10 2 30 4, 11 2 30 4, 10 2 30 4

using System;
using ILGPU;
using ILGPU.Runtime;

struct Pair
{
    public int A;
    public int B;
}

static class Kernels
{
    public static void RefSelectKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] local = new int[4];
        ref int target = ref (index.X & 1) == 0 ? ref local[1] : ref local[2];
        target = index.X + 1;
        output[index] = local[1] * 10 + local[2];
    }

    public static void RefSelectIndexKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] local = new int[4];
        ref int target = ref (index.X & 1) == 0
            ? ref local[0]
            : ref local[index.X & 3];
        target = index.X + 1;
        output[index] = local[0] * 10 + local[index.X & 3];
    }

    public static void RefFieldSelectKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        Pair[] local = new Pair[2];
        ref int target = ref (index.X & 1) == 0
            ? ref local[0].A
            : ref local[1].B;
        target = index.X + 1;
        output[index] = local[0].A * 10 + local[1].B;
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

        using var constant = stream.Allocate1D<int>(4);
        using var indexed = stream.Allocate1D<int>(4);
        using var field = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.RefSelectKernel(index, constant.View));
        stream.Launch(
            (Index1D)4,
            index => Kernels.RefSelectIndexKernel(index, indexed.View));
        stream.Launch(
            (Index1D)4,
            index => Kernels.RefFieldSelectKernel(index, field.View));
        stream.Synchronize();

        foreach (var v in constant.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in indexed.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in field.GetAsArray1D())
            Console.WriteLine(v);
    }
}
