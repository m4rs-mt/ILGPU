// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PredicateBarriers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PredicateBarriers
// Group and warp predicate barriers combine a predicate across all threads.
// 4 threads, input = [1, 0, 1, 1]: pop count 3, and false, or true at both
// levels, encoded as gpop gand gor wpop wand wor decimal digits.
// Expected output: 301301 301301 301301 301301

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PredicateBarriersKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        bool predicate = input[index] != 0;
        int groupPopCount = Group.BarrierPopCount(predicate);
        bool groupAnd = Group.BarrierAnd(predicate);
        bool groupOr = Group.BarrierOr(predicate);
        Group.MemoryFence();
        Warp.Barrier();
        int warpPopCount = Warp.BarrierPopCount(predicate);
        bool warpAnd = Warp.BarrierAnd(predicate);
        bool warpOr = Warp.BarrierOr(predicate);
        Grid.MemoryFence();
        Grid.SystemMemoryFence();
        output[index] =
            groupPopCount * 100000 + (groupAnd ? 10000 : 0) + (groupOr ? 1000 : 0) +
            warpPopCount * 100 + (warpAnd ? 10 : 0) + (warpOr ? 1 : 0);
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

        int[] inputData = [1, 0, 1, 1];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.PredicateBarriersKernel(index, input.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
