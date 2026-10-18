// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupedWideGroups.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupedWideGroups
// Explicitly grouped launches on a CPU device that emulates 32 threads per
// group, which is wider than the SIMD lanes of a vectorized call. A kernel
// that only reads its grid, group and warp indices runs groups of 32 and 20
// threads, whose warps must tile the group. A kernel that synchronizes its
// group with a barrier is rejected for groups wider than the SIMD lanes.
// Expected output: group 32 mismatches 0, group 20 mismatches 0,
// barrier rejected True

using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

static class Kernels
{
    public static void WideGroupKernel(
        KernelIndex index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        output[global * 4] = (int)index.GridIndex * 1000 + index.GroupIndex;
        output[global * 4 + 1] = (int)Grid.Index * 1000 + Group.Index;
        output[global * 4 + 2] = Warp.Index * Warp.Dimension + Warp.LaneIndex;
        output[global * 4 + 3] = (int)Grid.Dimension * 1000 + Group.Dimension;
    }

    public static void WideBarrierKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int count = Group.BarrierPopCount(true);
        output[(int)Grid.GlobalThreadIndex] = count;
    }
}

static class Program
{
    const int GridSize = 3;

    static void Run(AcceleratorStream stream, int groupSize)
    {
        int total = GridSize * groupSize;
        using var output = stream.Allocate1D<int>(total * 4);
        stream.Launch(new KernelConfig(GridSize, groupSize), index =>
            Kernels.WideGroupKernel(index, output.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        int mismatches = 0;
        for (int grid = 0; grid < GridSize; ++grid)
        {
            for (int group = 0; group < groupSize; ++group)
            {
                int offset = (grid * groupSize + group) * 4;
                if (values[offset] != grid * 1000 + group ||
                    values[offset + 1] != grid * 1000 + group ||
                    values[offset + 2] != group ||
                    values[offset + 3] != GridSize * 1000 + groupSize)
                {
                    ++mismatches;
                }
            }
        }
        Console.WriteLine($"group {groupSize} mismatches {mismatches}");
    }

    static void Main()
    {
        using var context = Context.Create(b => b.CPU(32));
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        Run(stream, 32);
        Run(stream, 20);

        bool rejected = false;
        try
        {
            using var output = stream.Allocate1D<int>(32);
            stream.Launch(new KernelConfig(1, 32), index =>
                Kernels.WideBarrierKernel(output.View));
            stream.Synchronize();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Console.WriteLine($"barrier rejected {rejected}");
    }
}
