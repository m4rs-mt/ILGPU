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

namespace SimpleAlloc;

static class Program
{
    const int AllocationSize1D = 512;
    const int AllocationSize2D = 256;
    const int AllocationSize3D = 128;

    static void SampleInitialization(AcceleratorStream stream)
    {
        using var data = stream.Allocate1D<int>(1024);
        data.MemSetToZero();
    }

    static void Alloc1D(AcceleratorStream stream, string name)
    {
        Console.WriteLine($"Performing 1D allocation on {name}");
        var data = Enumerable.Range(0, AllocationSize1D).ToArray();
        var targetData = new int[AllocationSize1D];
        using var buffer = stream.Allocate1D<int>(data.Length);

        buffer.CopyFromCPU(data);
        buffer.CopyToCPU(targetData);

        for (int i = 0; i < AllocationSize1D; ++i)
        {
            if (data[i] != targetData[i])
                Console.WriteLine($"Error comparing data and target data at {i}: {targetData[i]} found, but {data[i]} expected");
        }
    }

    static void Alloc2D(AcceleratorStream stream, string name)
    {
        Console.WriteLine($"Performing 2D allocation on {name}");
        var data = new int[AllocationSize1D, AllocationSize2D];
        for (int i = 0; i < AllocationSize1D; ++i)
        {
            for (int j = 0; j < AllocationSize2D; ++j)
                data[i, j] = j * AllocationSize1D + i;
        }
        var targetData = new int[AllocationSize1D, AllocationSize2D];
        using var buffer = stream.Allocate2DDenseY<int>(
            new LongIndex2D(AllocationSize1D, AllocationSize2D));

        buffer.CopyFromCPU(data);
        buffer.CopyToCPU(targetData);

        for (int i = 0; i < AllocationSize1D; ++i)
        {
            for (int j = 0; j < AllocationSize2D; ++j)
            {
                if (data[i, j] != targetData[i, j])
                    Console.WriteLine($"Error comparing data and target data at {i}, {j}: {targetData[i, j]} found, but {data[i, j]} expected");
            }
        }
    }

    static void Alloc3D(AcceleratorStream stream, string name)
    {
        Console.WriteLine($"Performing 3D allocation on {name}");
        var data = new int[AllocationSize1D, AllocationSize2D, AllocationSize3D];
        for (int i = 0; i < AllocationSize1D; ++i)
        {
            for (int j = 0; j < AllocationSize2D; ++j)
                for (int k = 0; k < AllocationSize3D; ++k)
                    data[i, j, k] = ((k * AllocationSize2D) + j) * AllocationSize1D + i;
        }
        var targetData = new int[AllocationSize1D, AllocationSize2D, AllocationSize3D];
        using var buffer = stream.Allocate3DDenseXY<int>(
            new LongIndex3D(AllocationSize1D, AllocationSize2D, AllocationSize3D));

        buffer.CopyFromCPU(data);
        buffer.CopyToCPU(targetData);

        for (int i = 0; i < AllocationSize1D; ++i)
        {
            for (int j = 0; j < AllocationSize2D; ++j)
            {
                for (int k = 0; k < AllocationSize3D; ++k)
                {
                    if (data[i, j, k] != targetData[i, j, k])
                        Console.WriteLine($"Error comparing data and target data at {i}, {j}, {k}: {targetData[i, j, k]} found, but {data[i, j, k]} expected");
                }
            }
        }
    }

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

        SampleInitialization(stream);
        Alloc1D(stream, accelerator.Name);
        Alloc2D(stream, accelerator.Name);
        Alloc3D(stream, accelerator.Name);

        Console.WriteLine("Done.");
    }
}
