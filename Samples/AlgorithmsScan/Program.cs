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

using System;
using System.Linq;
using ILGPU;
using ILGPU.Initialization;
using ILGPU.Runtime;
using ILGPU.ScanReduce;

namespace AlgorithmsScan;

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

        const int N = 32;
        using var sourceBuffer = stream.Allocate1D<int>(N);
        stream.Initialize(sourceBuffer.View, 2);

        // Inclusive scan
        using (var targetBuffer = stream.Allocate1D<int>(N))
        {
            stream.InclusiveScan<int, AddInt32>(sourceBuffer.View, targetBuffer.View);
            stream.Synchronize();

            Console.WriteLine("Inclusive Scan:");
            var data = targetBuffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }

        // Exclusive scan
        using (var targetBuffer = stream.Allocate1D<int>(N))
        {
            stream.ExclusiveScan<int, AddInt32>(sourceBuffer.View, targetBuffer.View);
            stream.Synchronize();

            Console.WriteLine("Exclusive Scan:");
            var data = targetBuffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }
    }
}
