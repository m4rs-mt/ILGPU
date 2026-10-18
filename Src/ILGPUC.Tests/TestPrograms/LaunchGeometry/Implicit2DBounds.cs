// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Implicit2DBounds.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: Implicit2DBounds
// Implicitly grouped 2D launch with odd extents in both dimensions, so they
// are not multiples of any block shape. The kernel writes a row-major index
// into a sub-view of a larger buffer; threads running past either extent
// either overwrite a neighbouring row (mismatch) or the canary tail
// (clobbered).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Implicit2DBoundsKernel(
        Index2D index, ArrayView1D<int, Stride1D.Dense> data, int width)
    {
        data[index.Y * width + index.X] = index.Y * 10000 + index.X;
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

        const int Width = 37;
        const int Height = 29;
        const int Canary = -1;
        const int Padding = 4096;

        var initial = new int[Width * Height + Padding];
        Array.Fill(initial, Canary);
        using var buffer = stream.Allocate1D(initial);
        var view = buffer.View.SubView(0, Width * Height);
        stream.Launch(
            new Index2D(Width, Height),
            index => Kernels.Implicit2DBoundsKernel(index, view, Width));
        stream.Synchronize();

        var values = buffer.GetAsArray1D();
        int mismatches = 0;
        for (int y = 0; y < Height; ++y)
        {
            for (int x = 0; x < Width; ++x)
            {
                if (values[y * Width + x] != y * 10000 + x)
                    ++mismatches;
            }
        }
        int clobbered = 0;
        for (int i = Width * Height; i < values.Length; ++i)
        {
            if (values[i] != Canary)
                ++clobbered;
        }

        Console.WriteLine($"mismatches {mismatches}");
        Console.WriteLine($"clobbered {clobbered}");
    }
}
