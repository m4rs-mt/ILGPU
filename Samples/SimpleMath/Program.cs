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

namespace SimpleMath;

static class Kernels
{
    public static void MathKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> singleView,
        ArrayView1D<double, Stride1D.Dense> doubleView,
        ArrayView1D<double, Stride1D.Dense> doubleView2)
    {
        singleView[index] = XMath.Abs(index);
        doubleView[index] = XMath.Clamp(index, 0.0, 12.0);
        doubleView2[index] = Math.Min(0.2, index);
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

        const int Length = 128;
        using var buffer = stream.Allocate1D<float>(Length);
        using var buffer2 = stream.Allocate1D<double>(Length);
        using var buffer3 = stream.Allocate1D<double>(Length);

        stream.Launch(
            (Index1D)Length,
            index => Kernels.MathKernel(
                index, buffer.View, buffer2.View, buffer3.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        var data2 = buffer2.GetAsArray1D();
        var data3 = buffer3.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
            Console.WriteLine($"Math results: {data[i]} (float) {data2[i]} (double [IntrinsicMath]) {data3[i]} (double [Math])");

        Console.WriteLine("Done.");
    }
}
