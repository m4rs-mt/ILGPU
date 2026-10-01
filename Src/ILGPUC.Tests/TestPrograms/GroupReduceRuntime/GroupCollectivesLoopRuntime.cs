// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupCollectivesLoopRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupCollectivesLoopRuntime
// Group reductions and scans, each alone in a loop over values that change in
// every iteration, in groups of as many warps as the device allows. Each call
// site reuses its shared memory in the next iteration, so a fast warp must not
// overwrite a slot that a slower warp has not read yet.
// Expected output: one line "<collective> mismatches 0" per collective.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public const int Count = 4;

    public static void GroupReduceLoopKernel(
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output,
        int iterations)
    {
        int index = (int)Grid.GlobalThreadIndex;
        int value = input[index];
        int result = 0;
        for (int i = 0; i < iterations; ++i)
            result += Group.Reduce(value * (i + 1) + i, int.MinValue, XMath.Max);
        output[index * Count] = result;
    }

    public static void GroupAllReduceLoopKernel(
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output,
        int iterations)
    {
        int index = (int)Grid.GlobalThreadIndex;
        int value = input[index];
        int result = 0;
        for (int i = 0; i < iterations; ++i)
            result += Group.AllReduce(value * (i + 1) + i, 0, (a, b) => a + b);
        output[index * Count + 1] = result;
    }

    public static void GroupInclusiveScanLoopKernel(
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output,
        int iterations)
    {
        int index = (int)Grid.GlobalThreadIndex;
        int value = input[index];
        int result = 0;
        for (int i = 0; i < iterations; ++i)
            result += Group.InclusiveScan(value * (i + 1) + i, 0, (a, b) => a + b);
        output[index * Count + 2] = result;
    }

    public static void GroupExclusiveScanLoopKernel(
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output,
        int iterations)
    {
        int index = (int)Grid.GlobalThreadIndex;
        int value = input[index];
        int result = 0;
        for (int i = 0; i < iterations; ++i)
            result += Group.ExclusiveScan(value * (i + 1) + i, 0, (a, b) => a + b);
        output[index * Count + 3] = result;
    }
}

static class Program
{
    static readonly string[] Names =
    [
        "group reduce max",
        "group all reduce add",
        "group inclusive scan add",
        "group exclusive scan add",
    ];

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        int groupSize = Math.Min(
            accelerator.MaxNumThreadsPerGroup,
            32 * accelerator.WarpSize);
        const int GridSize = 32;
        const int Iterations = 256;
        int total = GridSize * groupSize;
        var values = new int[total];
        for (int i = 0; i < total; ++i)
            values[i] = (i * 37 + i / 7) % 101 - 50;

        using var input = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(total * Kernels.Count);
        var config = new KernelConfig(GridSize, groupSize);
        stream.Launch(in config, index =>
            Kernels.GroupReduceLoopKernel(input.View, output.View, Iterations));
        stream.Launch(in config, index =>
            Kernels.GroupAllReduceLoopKernel(input.View, output.View, Iterations));
        stream.Launch(in config, index =>
            Kernels.GroupInclusiveScanLoopKernel(input.View, output.View, Iterations));
        stream.Launch(in config, index =>
            Kernels.GroupExclusiveScanLoopKernel(input.View, output.View, Iterations));
        stream.Synchronize();
        var results = output.GetAsArray1D();

        var mismatches = new int[Kernels.Count];
        for (int group = 0; group < total; group += groupSize)
        {
            var expected = new int[groupSize * Kernels.Count];
            for (int i = 0; i < Iterations; ++i)
            {
                int max = int.MinValue, sum = 0;
                for (int j = 0; j < groupSize; ++j)
                {
                    int current = values[group + j] * (i + 1) + i;
                    max = Math.Max(max, current);
                    expected[j * Kernels.Count + 3] += sum;
                    sum += current;
                    expected[j * Kernels.Count + 2] += sum;
                }
                for (int j = 0; j < groupSize; ++j)
                {
                    expected[j * Kernels.Count] += max;
                    expected[j * Kernels.Count + 1] += sum;
                }
            }
            for (int j = 0; j < groupSize; ++j)
            {
                for (int k = 0; k < Kernels.Count; ++k)
                {
                    // A group reduction is only valid in the first thread
                    if (k == 0 && j != 0)
                        continue;
                    int offset = (group + j) * Kernels.Count + k;
                    if (results[offset] != expected[j * Kernels.Count + k])
                        ++mismatches[k];
                }
            }
        }

        for (int k = 0; k < Kernels.Count; ++k)
            Console.WriteLine($"{Names[k]} mismatches {mismatches[k]}");
    }
}
