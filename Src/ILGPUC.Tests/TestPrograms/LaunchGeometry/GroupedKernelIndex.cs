// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupedKernelIndex.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupedKernelIndex
// Explicitly grouped launch of a kernel that takes a KernelIndex. The kernel
// writes a value encoding index.GridIndex / index.GroupIndex plus a scalar
// argument, and flags any thread whose KernelIndex disagrees with the
// Grid.Index / Group.Index intrinsics. Runs a small configuration (total
// threads below the group-size limit) and a large one (above it).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupedKernelIndexKernel(
        KernelIndex index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> disagreements,
        int constant)
    {
        int gridIndex = (int)index.GridIndex;
        int globalIndex = gridIndex * Group.Dimension + index.GroupIndex;
        data[globalIndex] = constant + gridIndex * 1000 + index.GroupIndex;
        disagreements[globalIndex] =
            gridIndex != Grid.Index || index.GroupIndex != Group.Index ? 1 : 0;
    }
}

static class Program
{
    static void Run(AcceleratorStream stream, int gridDim, int groupDim)
    {
        const int Constant = 7;
        var config = new KernelConfig(gridDim, groupDim);
        int total = gridDim * groupDim;

        using var data = stream.Allocate1D<int>(total);
        using var disagreements = stream.Allocate1D<int>(total);
        stream.Launch(in config, index =>
            Kernels.GroupedKernelIndexKernel(
                index, data.View, disagreements.View, Constant));
        stream.Synchronize();

        var values = data.GetAsArray1D();
        var flags = disagreements.GetAsArray1D();
        int mismatches = 0;
        int disagreeing = 0;
        for (int grid = 0; grid < gridDim; ++grid)
        {
            for (int group = 0; group < groupDim; ++group)
            {
                int i = grid * groupDim + group;
                if (values[i] != Constant + grid * 1000 + group)
                    ++mismatches;
                disagreeing += flags[i];
            }
        }

        Console.WriteLine($"config {gridDim}x{groupDim}");
        Console.WriteLine($"mismatches {mismatches}");
        Console.WriteLine($"disagreements {disagreeing}");
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        Run(stream, 4, 64);
        Run(stream, 8, 256);
    }
}
