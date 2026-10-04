// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupPermute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupPermute
// Group.Permute exchanges the values of the threads of a group in a random
// order. Every thread permutes its global index twice per iteration of a loop,
// with a random provider seeded per thread; the values of every group after
// each call have to be a permutation of the group's indices. Runs several
// group sizes, of several warps where the device allows.
// Expected output: permutation mismatches 0, moved True

using System;
using System.Collections.Generic;
using ILGPU;
using ILGPU.Random;
using ILGPU.Runtime;

static class Kernels
{
    public const int Iterations = 3;

    public static void GroupPermuteKernel(
        ArrayView1D<int, Stride1D.Dense> output,
        int seed)
    {
        int global = (int)Grid.GlobalThreadIndex;
        var random = new XorShift32((uint)(seed + global * 7919) | 1u);
        int total = (int)Grid.Dimension * Group.Dimension;
        for (int i = 0; i < Iterations; ++i)
        {
            int first = Group.Permute(global, ref random);
            int second = Group.Permute(-first - 1, ref random);
            output[(i * 2) * total + global] = first;
            output[(i * 2 + 1) * total + global] = -second - 1;
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

        int warpSize = accelerator.WarpSize;
        int maxGroupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, 1024);
        var groupSizes = new SortedSet<int>();
        foreach (int size in new[]
        {
            Math.Max(warpSize / 2, 1), warpSize, warpSize * 4, maxGroupSize,
        })
        {
            if (size <= maxGroupSize)
                groupSizes.Add(size);
        }

        const int GridSize = 3;
        int mismatches = 0;
        bool moved = false;
        foreach (int groupSize in groupSizes)
        {
            int total = GridSize * groupSize;
            int rounds = Kernels.Iterations * 2;
            using var output = stream.Allocate1D<int>(rounds * total);
            stream.Launch(new KernelConfig(GridSize, groupSize), index =>
                Kernels.GroupPermuteKernel(output.View, groupSize));
            stream.Synchronize();

            var values = output.GetAsArray1D();
            for (int round = 0; round < rounds; ++round)
            {
                for (int group = 0; group < GridSize; ++group)
                {
                    int start = group * groupSize;
                    var seen = new bool[groupSize];
                    for (int lane = 0; lane < groupSize; ++lane)
                    {
                        int value = values[round * total + start + lane] - start;
                        if (value < 0 || value >= groupSize || seen[value])
                            ++mismatches;
                        else
                            seen[value] = true;
                        if (value != lane)
                            moved = true;
                    }
                }
            }
        }

        Console.WriteLine($"permutation mismatches {mismatches}");
        Console.WriteLine($"moved {moved}");
    }
}
