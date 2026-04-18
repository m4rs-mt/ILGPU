// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace GroupGridIndices;

static class Kernels
{
    /// <summary>
    /// Computes the globally unique thread index from group/grid indices
    /// and writes a constant value.
    /// </summary>
    public static void GroupGridIndexKernel(
        ArrayView1D<int, Stride1D.Dense> data,
        int constant)
    {
        var globalIndex = Group.Dimension * Grid.Index + Group.Index;

        if (globalIndex < data.Length)
            data[globalIndex] = constant;
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.Devices
            .OrderByDescending(d => d.AcceleratorType switch
            {
                AcceleratorType.Metal  => 5,
                AcceleratorType.Cuda   => 4,
                AcceleratorType.ROCm   => 3,
                AcceleratorType.OpenCL => 2,
                AcceleratorType.CPU    => 1,
                _                      => 0,
            })
            .First();

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        var groupSize = accelerator.MaxNumThreadsPerGroup;
        var config = new KernelConfig(gridDim: 2, groupDim: groupSize);

        using var buffer = stream.Allocate1D<int>((long)config.GridSize * config.GroupSize);

        stream.Launch(in config, index =>
            Kernels.GroupGridIndexKernel(buffer.View, 64));
        stream.Synchronize();

        Console.WriteLine("Default grouped kernel");
        var data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"Data[{i}] = {data[i]}");

        Console.WriteLine("Done.");
    }
}
