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

namespace SimpleWriteLine;

static class Kernels
{
    public static void WriteLineKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> dataView)
    {
        Interop.WriteLine("{0} = {1}", index, dataView[index]);
    }
}

static class Program
{
    static void Main()
    {
        var values = Enumerable.Range(0, 16).ToArray();

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
        Console.WriteLine($"Performing operations on {accelerator}");
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D(values);

        stream.Launch(
            (Index1D)buffer.Length,
            index => Kernels.WriteLineKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine("Done.");
    }
}
