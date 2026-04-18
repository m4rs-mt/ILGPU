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
using ILGPU.Runtime;

#pragma warning disable CA1034
#pragma warning disable CA1051

namespace InterleaveFields;

public struct MyPoint
{
    public int X;
    public int Y;
}

// Manually implemented (the [InterleaveFields] source generator is not yet available)
public struct MyPoint4
{
    public IntBuffer4 X;
    public IntBuffer4 Y;
}

[System.Runtime.CompilerServices.InlineArray(4)]
public struct IntBuffer4
{
    private int _element0;
}

static class Kernels
{
    public static unsafe void MyKernel(
        Index1D index,
        ArrayView1D<MyPoint4, Stride1D.Dense> dataView)
    {
        dataView[index].X[0] = index;
        dataView[index].X[1] = index + 1;
        dataView[index].X[2] = index + 2;
        dataView[index].X[3] = index + 3;
        dataView[index].Y[0] = index + 4;
        dataView[index].Y[1] = index + 5;
        dataView[index].Y[2] = index + 6;
        dataView[index].Y[3] = index + 7;
    }
}

static class Program
{
    static unsafe void Main()
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

        const int Length = 1024;
        using var buffer = stream.Allocate1D<MyPoint4>(Length);

        stream.Launch(
            (Index1D)Length,
            index => Kernels.MyKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0, e = data.Length; i < e; ++i)
        {
            if (data[i].X[0] != i
                || data[i].X[1] != i + 1
                || data[i].X[2] != i + 2
                || data[i].X[3] != i + 3
                || data[i].Y[0] != i + 4
                || data[i].Y[1] != i + 5
                || data[i].Y[2] != i + 6
                || data[i].Y[3] != i + 7)
                Console.WriteLine($"Error at element location {i}");
        }

        Console.WriteLine("Done.");
    }
}

#pragma warning restore CA1034
#pragma warning restore CA1051
