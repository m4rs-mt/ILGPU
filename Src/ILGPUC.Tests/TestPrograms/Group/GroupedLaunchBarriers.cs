// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupedLaunchBarriers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupedLaunchBarriers
// Explicitly grouped launches of kernels with and without a KernelIndex that
// combine a per-thread predicate with group predicate barriers and exchange
// values with their neighbours through shared memory. Every thread records the
// grid and group indices and dimensions it sees and the barrier results; the
// host recomputes them for each group. Runs a full group (up to 64 threads)
// and a group of 3 threads, and checks that a group larger than the device
// allows is rejected.
// Expected output: per size, the mismatches of both kernels, then whether the
// oversized group was rejected with an error reporting the requested group size.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    const int MaxGroupSize = 64;

    static void Record(
        int gridIndex,
        int groupIndex,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = gridIndex * Group.Dimension + groupIndex;
        bool predicate = input[global] != 0;
        int popCount = Group.BarrierPopCount(predicate);
        bool all = Group.BarrierAnd(predicate);
        bool any = Group.BarrierOr(predicate);

        var shared = Group.GetSharedMemory<int>(MaxGroupSize);
        shared[groupIndex] = input[global] * 100 + groupIndex;
        Group.Barrier();
        int neighbour = shared[(groupIndex + 1) % Group.Dimension];

        int offset = global * 6;
        output[offset] = gridIndex * 1000 + groupIndex;
        output[offset + 1] = (int)Grid.Dimension * 1000 + Group.Dimension;
        output[offset + 2] = popCount * 4 + (all ? 2 : 0) + (any ? 1 : 0);
        output[offset + 3] = neighbour;
        output[offset + 4] = (int)Grid.Index * 1000 + Group.Index;
        output[offset + 5] = (int)Grid.GlobalThreadIndex;
    }

    public static void GroupedBarriersKernel(
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output) =>
        Record((int)Grid.Index, Group.Index, input, output);

    public static void KernelIndexBarriersKernel(
        KernelIndex index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output) =>
        Record((int)index.GridIndex, index.GroupIndex, input, output);
}

static class Program
{
    const int GridSize = 7;

    static int Verify(int[] input, int[] values, int groupSize)
    {
        int mismatches = 0;
        for (int grid = 0; grid < GridSize; ++grid)
        {
            int start = grid * groupSize;
            int popCount = 0;
            for (int i = 0; i < groupSize; ++i)
                popCount += input[start + i] != 0 ? 1 : 0;
            int barriers = popCount * 4 + (popCount == groupSize ? 2 : 0) +
                (popCount > 0 ? 1 : 0);

            for (int group = 0; group < groupSize; ++group)
            {
                int global = start + group;
                int next = (group + 1) % groupSize;
                int offset = global * 6;
                if (values[offset] != grid * 1000 + group ||
                    values[offset + 1] != GridSize * 1000 + groupSize ||
                    values[offset + 2] != barriers ||
                    values[offset + 3] != input[start + next] * 100 + next ||
                    values[offset + 4] != grid * 1000 + group ||
                    values[offset + 5] != global)
                {
                    ++mismatches;
                }
            }
        }
        return mismatches;
    }

    static void Run(AcceleratorStream stream, int groupSize)
    {
        int total = GridSize * groupSize;
        var inputData = new int[total];
        for (int i = 0; i < total; i++)
        {
            // Group 0 all true, group 1 all false, the rest a mixed pattern
            inputData[i] = i < groupSize ? 1
                : i < 2 * groupSize ? 0
                : (i * 7 + i / 3) % 5 < 2 ? 1 : 0;
        }

        var config = new KernelConfig(GridSize, groupSize);
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(total * 6);

        stream.Launch(in config, index =>
            Kernels.GroupedBarriersKernel(input.View, output.View));
        stream.Synchronize();
        int grouped = Verify(inputData, output.GetAsArray1D(), groupSize);

        output.MemSetToZero(stream);
        stream.Launch(in config, index =>
            Kernels.KernelIndexBarriersKernel(index, input.View, output.View));
        stream.Synchronize();
        int kernelIndex = Verify(inputData, output.GetAsArray1D(), groupSize);

        Console.WriteLine($"grouped mismatches {grouped}");
        Console.WriteLine($"kernel index mismatches {kernelIndex}");
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        int maxGroupSize = accelerator.MaxNumThreadsPerGroup;
        Run(stream, Math.Min(64, maxGroupSize));
        Run(stream, Math.Min(3, maxGroupSize));

        bool rejected = false;
        try
        {
            using var input = stream.Allocate1D<int>(maxGroupSize + 1);
            using var output = stream.Allocate1D<int>((maxGroupSize + 1) * 6);
            stream.Launch(new KernelConfig(1, maxGroupSize + 1), index =>
                Kernels.GroupedBarriersKernel(input.View, output.View));
            stream.Synchronize();
        }
        catch (InvalidOperationException exception)
        {
            // The error reports the requested group size
            rejected = exception.Message.StartsWith(
                $"Invalid group dimensions {maxGroupSize + 1} (exceeds maximum ",
                StringComparison.Ordinal);
        }
        Console.WriteLine($"oversized rejected {rejected}");
    }
}
