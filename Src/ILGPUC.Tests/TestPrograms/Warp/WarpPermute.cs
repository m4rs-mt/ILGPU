// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpPermute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WarpPermute
// Warp.Permute exchanges the values of the lanes of a warp in a random order.
// Every thread permutes its global index in a loop, with a random provider
// seeded per thread; the values of every warp after each call have to be a
// permutation of the warp's indices. Runs groups of several warps where the
// device allows.
// Expected output: permutation mismatches 0, moved True

using System;
using ILGPU;
using ILGPU.Random;
using ILGPU.Runtime;

static class Kernels
{
    public const int Iterations = 4;

    public static void WarpPermuteKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        var random = new XorShift32((uint)(global * 7919 + 17) | 1u);
        int total = (int)Grid.Dimension * Group.Dimension;
        for (int i = 0; i < Iterations; ++i)
            output[i * total + global] = Warp.Permute(global, ref random);
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

        int warpSize = accelerator.WarpSize;
        int groupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, warpSize * 4);
        const int GridSize = 3;
        int total = GridSize * groupSize;
        using var output = stream.Allocate1D<int>(Kernels.Iterations * total);
        stream.Launch(new KernelConfig(GridSize, groupSize), index =>
            Kernels.WarpPermuteKernel(output.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        int mismatches = 0;
        bool moved = false;
        for (int round = 0; round < Kernels.Iterations; ++round)
        {
            for (int start = 0; start < total; start += warpSize)
            {
                var seen = new bool[warpSize];
                for (int lane = 0; lane < warpSize; ++lane)
                {
                    int value = values[round * total + start + lane] - start;
                    if (value < 0 || value >= warpSize || seen[value])
                        ++mismatches;
                    else
                        seen[value] = true;
                    if (value != lane)
                        moved = true;
                }
            }
        }

        Console.WriteLine($"permutation mismatches {mismatches}");
        Console.WriteLine($"moved {moved}");
    }
}
