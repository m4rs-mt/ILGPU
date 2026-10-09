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

namespace AlgorithmsMath;

static class Kernels
{
    public static void KernelWithXMath(Index1D index, ArrayView<float> data, float c)
    {
        data[index] = XMath.Sinh(c + index) + XMath.Atan(c);
    }

    public static void KernelWithMath(Index1D index, ArrayView<float> data, float c)
    {
        data[index] = (float)(Math.Sinh(c + index) + Math.Atan(c));
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

        const int Length = 64;
        using var buffer = stream.Allocate1D<float>(Length);

        Console.WriteLine(nameof(Kernels.KernelWithXMath));
        stream.Launch(
            (Index1D)Length,
            index => Kernels.KernelWithXMath(index, buffer.View, 0.1f));
        stream.Synchronize();
        WriteData(buffer);

        Console.WriteLine(nameof(Kernels.KernelWithMath));
        stream.Launch(
            (Index1D)Length,
            index => Kernels.KernelWithMath(index, buffer.View, 0.1f));
        stream.Synchronize();
        WriteData(buffer);
    }

    static void WriteData(MemoryBuffer1D<float, Stride1D.Dense> buffer)
    {
        var data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"Data[{i}] = {data[i]}");
    }
}
