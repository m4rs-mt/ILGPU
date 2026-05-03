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

namespace SimpleAtomics;

static class Kernels
{
    public static void AtomicOperationKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> dataView,
        int constant)
    {
        Atomic.Add(ref dataView[0], constant);
        Atomic.Max(ref dataView[1], constant);
        Atomic.Min(ref dataView[2], constant);
        Atomic.And(ref dataView[3], constant);
        Atomic.Or(ref dataView[4], constant);
        Atomic.Xor(ref dataView[5], constant);
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
        Console.WriteLine($"Performing operations on {accelerator}");
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(7);
        buffer.MemSetToZero();

        stream.Launch(
            (Index1D)1024,
            index => Kernels.AtomicOperationKernel(index, buffer.View, 4));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"Data[{i}] = {data[i]}");

        Console.WriteLine("Done.");
    }
}
