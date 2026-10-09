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

namespace AdvancedAtomics;

static class Kernels
{
    // Custom atomic add for doubles using MakeAtomic with delegate operations
    public static void AddDoubleAtomicKernel(
        Index1D index,
        ArrayView<double> dataView,
        double value)
    {
        Atomic.MakeAtomic(
            ref dataView[0],
            value,
            (current, val) => current + val,
            (ref double target, double compare, double val) =>
                Atomic.CompareExchange(ref target, compare, val));
    }

    // Using built-in Atomic.Add for doubles
    public static void AddDoubleBuiltInKernel(
        Index1D index,
        ArrayView<double> dataView,
        double value)
    {
        Atomic.Add(ref dataView[0], value);
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

        // Custom MakeAtomic kernel
        {
            Console.WriteLine("Launching: AddDoubleAtomicKernel");
            using var buffer = stream.Allocate1D<double>(1);
            buffer.MemSetToZero();

            stream.Launch(
                (Index1D)1024,
                index => Kernels.AddDoubleAtomicKernel(index, buffer.View, 2.0));
            stream.Synchronize();

            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }

        // Built-in Atomic.Add kernel
        {
            Console.WriteLine("Launching: AddDoubleBuiltInKernel");
            using var buffer = stream.Allocate1D<double>(1);
            buffer.MemSetToZero();

            stream.Launch(
                (Index1D)1024,
                index => Kernels.AddDoubleBuiltInKernel(index, buffer.View, 2.0));
            stream.Synchronize();

            var data = buffer.GetAsArray1D();
            for (int i = 0, e = data.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {data[i]}");
        }
    }
}
