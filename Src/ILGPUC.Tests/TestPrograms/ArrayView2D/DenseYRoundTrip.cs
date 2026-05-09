// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DenseYRoundTrip.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DenseYRoundTrip — pins ArrayView2D<T, Stride2D.DenseY> indexing
// and linear-layout semantics for #1464.
//
// Same scenario as DenseXRoundTrip but with the orthogonal stride. The
// view[Index2D(x, y)] addressing on the kernel side stays identical, but the
// linear memory layout swaps: for Stride2D.DenseY the linear offset of
// view[Index2D(x, y)] is x * H + y (Y is the contiguous axis). This makes the
// stride choice invisible at the indexer level but visible in the host-side
// flat read-back, which is exactly the contract a user porting CPU code needs
// to understand.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DenseYKernel(
        Index2D index,
        ArrayView2D<int, Stride2D.DenseY> view)
    {
        view[index] = index.X * 10 + index.Y;
    }
}

static class Program
{
    static void Main()
    {
        const int W = 3;
        const int H = 2;

        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(W * H);
        var view2d = buffer.View.As2DDenseYView(new Index2D(W, H));

        stream.Launch(
            new Index2D(W, H),
            index => Kernels.DenseYKernel(index, view2d));
        stream.Synchronize();

        var flat = buffer.GetAsArray1D();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                Console.WriteLine($"x={x},y={y}:{flat[x * H + y]}");
    }
}
