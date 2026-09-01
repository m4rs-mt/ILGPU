// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LongIndexLaunches.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LongIndexLaunches
// Implicitly grouped launches over long extents: LongIndex1D, LongIndex2D and
// LongIndex3D. Every thread writes a value derived from its own index, so a
// thread that sees the index of another thread of its group leaves a wrong
// value behind. The extents are not multiples of any group size.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Long1DKernel(
        LongIndex1D index, ArrayView1D<long, Stride1D.Dense> data, long bias)
    {
        data[index] = index.X * 3 + bias;
    }

    public static void Long1DCopyKernel(
        LongIndex1D index, ArrayView1D<long, Stride1D.Dense> data)
    {
        data[index] = index;
    }

    public static void Long2DKernel(
        LongIndex2D index, ArrayView1D<long, Stride1D.Dense> data, long width)
    {
        data[index.Y * width + index.X] = index.Y * 100 + index.X;
    }

    public static void Long3DKernel(
        LongIndex3D index,
        ArrayView1D<long, Stride1D.Dense> data,
        long width,
        long height)
    {
        data[(index.Z * height + index.Y) * width + index.X] =
            index.Z * 10000 + index.Y * 100 + index.X;
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        const long Length = 19;
        using var buffer1D = stream.Allocate1D<long>(Length);
        var view1D = buffer1D.View;
        long bias = 7;
        stream.Launch(
            new LongIndex1D(Length),
            index => Kernels.Long1DKernel(index, view1D, bias));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer1D.GetAsArray1D()));

        stream.Launch(
            new LongIndex1D(Length),
            index => Kernels.Long1DCopyKernel(index, view1D));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer1D.GetAsArray1D()));

        const long Width = 5;
        const long Height = 3;
        using var buffer2D = stream.Allocate1D<long>(Width * Height);
        var view2D = buffer2D.View;
        stream.Launch2D(
            new LongIndex2D(Width, Height),
            index => Kernels.Long2DKernel(index, view2D, Width));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer2D.GetAsArray1D()));

        const long Depth = 2;
        using var buffer3D = stream.Allocate1D<long>(Width * Height * Depth);
        var view3D = buffer3D.View;
        stream.Launch3D(
            new LongIndex3D(Width, Height, Depth),
            index => Kernels.Long3DKernel(index, view3D, Width, Height));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer3D.GetAsArray1D()));
    }
}
