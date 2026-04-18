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

namespace AlgorithmsInitialize;

public struct CustomStruct
{
    public int First { get; set; }
    public int Second { get; set; }

    public override string ToString() =>
        $"First: {First}, Second: {Second}";
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

        // Initialize a buffer of ints to a constant value
        using (var buffer = stream.Allocate1D<int>(64))
        {
            stream.Initialize(buffer.View, 23);
            stream.Synchronize();

            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }

        // Initialize a buffer of custom structs using a lambda
        using (var buffer = stream.Allocate1D<CustomStruct>(64))
        {
            stream.Initialize(buffer.View, _ => new CustomStruct
            {
                First = 23,
                Second = 42
            });
            stream.Synchronize();

            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data2[{i}] = {data[i]}");
        }
    }
}
