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
using ILGPU;
using ILGPU.Runtime;

namespace DeviceInfo;

static class Program
{
    static void PrintAcceleratorInfo(Accelerator accelerator)
    {
        Console.WriteLine($"Name: {accelerator.Name}");
        Console.WriteLine($"MemorySize: {accelerator.MemorySize}");
        Console.WriteLine($"MaxThreadsPerGroup: {accelerator.MaxNumThreadsPerGroup}");
        Console.WriteLine($"MaxSharedMemoryPerGroup: {accelerator.MaxSharedMemoryPerGroup}");
        Console.WriteLine($"MaxConstantMemory: {accelerator.MaxConstantMemory}");
        Console.WriteLine($"WarpSize: {accelerator.WarpSize}");
        Console.WriteLine($"NumMultiprocessors: {accelerator.NumMultiprocessors}");
    }

    static void Main()
    {
        // Enumerate all available devices
        using var context = Context.CreateDefault();

        foreach (var device in context)
        {
            using var accelerator = device.CreateAccelerator(context);
            Console.WriteLine($"Accelerator: {device.AcceleratorType}, {accelerator.Name}");
            PrintAcceleratorInfo(accelerator);
            Console.WriteLine();
        }
    }
}
