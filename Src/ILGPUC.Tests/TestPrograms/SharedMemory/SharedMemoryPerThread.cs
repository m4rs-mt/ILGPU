// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryPerThread.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: SharedMemoryPerThread
// Group.GetSharedMemoryPerThread allocates one element per thread of the group,
// its overload with a multiplier several elements per thread, and
// Group.GetSharedMemoryPerWarp one element per warp. Every thread writes its
// own slots and reads those of its neighbour; the first lane of every warp
// writes the slot of its warp and every thread reads the slot of the next
// warp. The multiplier buffer lives in a helper that is not inlined. Runs
// several group sizes, of several warps where the device allows.
// Expected output: per thread mismatches 0, multiplier mismatches 0,
// per warp mismatches 0

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public const int Outputs = 6;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static long ExchangePairs(int global, out int length)
    {
        var pairs = Group.GetSharedMemoryPerThread<long>(2);
        int index = Group.Index;
        pairs[index * 2] = global * 3L;
        pairs[index * 2 + 1] = -global;
        Group.Barrier();
        int next = (index + 1) % Group.Dimension;
        long result = pairs[next * 2] * 4L + pairs[next * 2 + 1];
        length = pairs.IntLength;
        return result;
    }

    public static void PerThreadKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        var values = Group.GetSharedMemoryPerThread<int>();
        var perWarp = Group.GetSharedMemoryPerWarp<int>();
        int global = (int)Grid.GlobalThreadIndex;
        int index = Group.Index;
        int numWarps = (Group.Dimension + Warp.Dimension - 1) / Warp.Dimension;

        values[index] = global;
        if (Warp.IsFirstLane)
            perWarp[Warp.Index] = (int)Grid.Index * 1000 + Warp.Index;
        Group.Barrier();

        int offset = global * Outputs;
        output[offset] = values[(index + 1) % Group.Dimension];
        output[offset + 1] = values.IntLength;
        output[offset + 2] = perWarp[(Warp.Index + 1) % numWarps];
        output[offset + 3] = perWarp.IntLength;

        long pair = ExchangePairs(global, out int pairsLength);
        output[offset + 4] = (int)pair;
        output[offset + 5] = pairsLength;
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
        int perThread = 0;
        int multiplier = 0;
        int perWarp = 0;
        foreach (int groupSize in groupSizes)
        {
            int total = GridSize * groupSize;
            using var output = stream.Allocate1D<int>(total * Kernels.Outputs);
            stream.Launch(new KernelConfig(GridSize, groupSize), index =>
                Kernels.PerThreadKernel(output.View));
            stream.Synchronize();

            var values = output.GetAsArray1D();
            int numWarps = (groupSize + warpSize - 1) / warpSize;
            for (int i = 0; i < total; ++i)
            {
                int group = i / groupSize;
                int lane = i % groupSize;
                int next = i - lane + (lane + 1) % groupSize;
                int warp = lane / warpSize;
                int offset = i * Kernels.Outputs;
                if (values[offset] != next || values[offset + 1] != groupSize)
                    ++perThread;
                if (values[offset + 2] != group * 1000 + (warp + 1) % numWarps ||
                    values[offset + 3] < numWarps)
                {
                    ++perWarp;
                }
                if (values[offset + 4] != next * 12 - next ||
                    values[offset + 5] != groupSize * 2)
                {
                    ++multiplier;
                }
            }
        }

        Console.WriteLine($"per thread mismatches {perThread}");
        Console.WriteLine($"multiplier mismatches {multiplier}");
        Console.WriteLine($"per warp mismatches {perWarp}");
    }
}
