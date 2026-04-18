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

namespace GenericKernel;

interface IKernelFunction<T>
    where T : struct
{
    T ComputeValue(Index1D index, int value);
}

readonly struct LambdaClosure : IKernelFunction<long>
{
    public LambdaClosure(long offset)
    {
        Offset = offset;
    }

    public long Offset { get; }

    public long ComputeValue(Index1D index, int value) =>
        Offset + value * index;
}

static class Kernels
{
    public static void GenericKernel<TKernelFunction, T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        int value,
        TKernelFunction function)
        where TKernelFunction : struct, IKernelFunction<T>
        where T : unmanaged
    {
        data[index] = function.ComputeValue(index, value);
    }
}

static class Program
{
    static void Main()
    {
        const int DataSize = 1024;

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

        using var buffer = stream.Allocate1D<long>(DataSize);

        var closure = new LambdaClosure(20);
        stream.Launch(
            (Index1D)DataSize,
            index => Kernels.GenericKernel(index, buffer.View, 1, closure));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0; i < data.Length; i++)
        {
            long expected = 20 + 1 * i;
            if (data[i] != expected)
                Console.WriteLine($"Error at element {i}: {data[i]} found, expected {expected}");
        }

        Console.WriteLine("Done.");
    }
}
