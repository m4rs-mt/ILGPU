// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupRadixSortGroupSizes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupRadixSortGroupSizes
// Group.RadixSort sorts the values of all threads of a group, read from a
// buffer in a scrambled order. The values of every group have to come back in
// ascending order and as a permutation of the group's inputs. Runs several
// group sizes, of several warps where the device allows.
// Expected output: unsorted 0, lost 0

using System;
using System.Collections.Generic;
using ILGPU;
using ILGPU.RadixSort;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupRadixSortGroupSizesKernel(
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        output[global] = Group.RadixSort<int, AscendingInt32>(input[global]);
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
            Math.Max(warpSize / 2, 1), warpSize, warpSize * 3, warpSize * 8,
            maxGroupSize,
        })
        {
            if (size <= maxGroupSize)
                groupSizes.Add(size);
        }

        const int GridSize = 3;
        int unsorted = 0;
        int lost = 0;
        foreach (int groupSize in groupSizes)
        {
            int total = GridSize * groupSize;
            var inputData = new int[total];
            for (int i = 0; i < total; ++i)
                inputData[i] = (int)((i * 2654435761L) % 100003) - 50000;
            using var input = stream.Allocate1D(inputData);
            using var output = stream.Allocate1D<int>(total);
            stream.Launch(new KernelConfig(GridSize, groupSize), index =>
                Kernels.GroupRadixSortGroupSizesKernel(input.View, output.View));
            stream.Synchronize();

            var values = output.GetAsArray1D();
            for (int group = 0; group < GridSize; ++group)
            {
                int start = group * groupSize;
                var expected = inputData[start..(start + groupSize)];
                Array.Sort(expected);
                for (int lane = 0; lane < groupSize; ++lane)
                {
                    if (lane > 0 && values[start + lane] < values[start + lane - 1])
                        ++unsorted;
                    if (values[start + lane] != expected[lane])
                        ++lost;
                }
            }
        }

        Console.WriteLine($"unsorted {unsorted}");
        Console.WriteLine($"lost {lost}");
    }
}
