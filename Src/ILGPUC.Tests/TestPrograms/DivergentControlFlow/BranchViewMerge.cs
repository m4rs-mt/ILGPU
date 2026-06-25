// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BranchViewMerge.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: BranchViewMerge
// Lanes return early or take one of two branches with side effects, and the
// output view is written after the branches merge. The IR merges the view in
// a phi of two copies rebuilt from the same kernel parameter.
// Counters = [5, 1, 0, 3, 0, 1]; lanes with index % 3 == 0 return, even lanes
// compute 5 * 4 + 2 = 22, odd lanes 1000 + 3 * 4 + 1 = 1013.
// Expected output: 0 1013 22 0 22 1013 0 1013 22 0 22 1013

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void BranchViewMergeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> counters,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        if (index % 3 == 0)
            return;

        int result;
        if (index % 2 == 0)
        {
            result = Atomic.Add(ref counters[0], 0) * 4 +
                (Atomic.Add(ref counters[1], 0) != 0 ? 2 : 0) +
                (Atomic.Add(ref counters[2], 0) != 0 ? 1 : 0);
        }
        else
        {
            result = 1000 + Atomic.Add(ref counters[3], 0) * 4 +
                (Atomic.Add(ref counters[4], 0) != 0 ? 2 : 0) +
                (Atomic.Add(ref counters[5], 0) != 0 ? 1 : 0);
        }
        output[index] = result;
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

        using var counters = stream.Allocate1D(new int[] { 5, 1, 0, 3, 0, 1 });
        using var output = stream.Allocate1D<int>(12);
        output.MemSetToZero(stream);
        stream.Launch(
            (Index1D)12,
            index => Kernels.BranchViewMergeKernel(index, counters.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
