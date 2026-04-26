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

namespace FixedSizeBuffers;

public unsafe struct CustomFixedBufferStruct
{
    public fixed double Block1[2];
    public fixed int Block2[3];

    public override string ToString() =>
        $"[({Block1[0]}, {Block1[1]}), ({Block2[0]}, {Block2[1]}, {Block2[2]})]";
}

static class Kernels
{
    public static unsafe void MyKernel(
        Index1D index,
        ArrayView1D<CustomFixedBufferStruct, Stride1D.Dense> view)
    {
        view[index].Block1[0] = 11;
        view[index].Block1[1] = 22;
        view[index].Block2[0] = 33;
        view[index].Block2[1] = 44;
        view[index].Block2[2] = 55;
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

        const int Length = 16;
        using var buffer = stream.Allocate1D<CustomFixedBufferStruct>(Length);
        buffer.MemSetToZero();

        stream.Launch(
            (Index1D)Length,
            index => Kernels.MyKernel(index, buffer.View));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0, e = result.Length; i < e; ++i)
            Console.WriteLine($"Result[{i}] = {result[i]}");

        Console.WriteLine("Done.");
    }
}
