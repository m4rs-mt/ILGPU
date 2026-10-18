// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupedImplicitIndex.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupedImplicitIndex
// Explicitly grouped launch of a kernel without an index parameter. Every
// thread derives its position from Grid.Index / Group.Index and writes a value
// that encodes both, plus a scalar argument, so a collapsed grid, wrong
// dimensions or a shifted argument list all show up as mismatches. Runs a
// small and a large configuration, with group sizes clamped to the maximum
// group size of the device.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupedImplicitIndexKernel(
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> dims,
        int constant)
    {
        int gridIndex = (int)Grid.Index;
        int globalIndex = Group.Dimension * gridIndex + Group.Index;
        data[globalIndex] = constant + gridIndex * 1000 + Group.Index;
        if (globalIndex == 0)
        {
            dims[0] = (int)Grid.Dimension;
            dims[1] = Group.Dimension;
        }
    }
}

static class Program
{
    static void Run(AcceleratorStream stream, int gridDim, int requestedGroupDim)
    {
        // Groups never exceed the device's maximum group size
        int groupDim = Math.Min(
            requestedGroupDim,
            stream.Accelerator!.MaxNumThreadsPerGroup);
        const int Constant = 7;
        var config = new KernelConfig(gridDim, groupDim);
        int total = gridDim * groupDim;

        using var data = stream.Allocate1D<int>(total);
        using var dims = stream.Allocate1D<int>(2);
        stream.Launch(in config, index =>
            Kernels.GroupedImplicitIndexKernel(data.View, dims.View, Constant));
        stream.Synchronize();

        var values = data.GetAsArray1D();
        int mismatches = 0;
        for (int grid = 0; grid < gridDim; ++grid)
        {
            for (int group = 0; group < groupDim; ++group)
            {
                if (values[grid * groupDim + group] != Constant + grid * 1000 + group)
                    ++mismatches;
            }
        }

        var dimValues = dims.GetAsArray1D();
        Console.WriteLine($"config {gridDim}x{requestedGroupDim}");
        int reportedGroupDim =
            dimValues[1] == groupDim ? requestedGroupDim : dimValues[1];
        Console.WriteLine($"grid {dimValues[0]} group {reportedGroupDim}");
        Console.WriteLine($"mismatches {mismatches}");
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
