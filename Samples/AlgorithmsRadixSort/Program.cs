// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
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
using ILGPU.RadixSort;
using ILGPU.Runtime;

namespace AlgorithmsRadixSort;

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
        using var buffer = stream.Allocate1D<int>(N);

        // Fill with a descending sequence: N-1, N-2, ..., 1, 0
        stream.Sequence(buffer.View, i => (int)(N - 1 - i));

        // Ascending radix sort
        stream.RadixSort<int, AscendingInt32>(buffer.View);
        stream.Synchronize();

        Console.WriteLine("Ascending RadixSort:");
        var data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"Data[{i}] = {data[i]}");

        // Descending radix sort
        stream.RadixSort<int, DescendingInt32>(buffer.View);
        stream.Synchronize();

        Console.WriteLine("Descending RadixSort:");
        data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"Data[{i}] = {data[i]}");
    }
}
