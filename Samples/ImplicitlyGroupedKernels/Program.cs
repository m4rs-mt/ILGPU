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
using ILGPU.Runtime;

namespace ImplicitlyGroupedKernels;

static class Kernels
{
    public static void AddConstantKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int constant)
    {
        data[index] = index + constant;
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

        const int Length = 1024;
        using var buffer = stream.Allocate1D<int>(Length);

        // Launch an implicitly grouped kernel. The runtime automatically
        // determines grid/group dimensions from the extent.
        stream.Launch(
            (Index1D)Length,
            index => Kernels.AddConstantKernel(index, buffer.View, 42));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] != 42 + i)
                Console.WriteLine($"Error at element {i}: {data[i]} found");
        }

        Console.WriteLine("Done.");
    }
}
