// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DenseXRoundTrip.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DenseXRoundTrip — pins ArrayView2D<T, Stride2D.DenseX> indexing
// and linear-layout semantics raised in #1464.
//
// The kernel writes a position-encoded value (index.X * 10 + index.Y) at every
// (x, y) in a (W=3, H=2) view. The host then re-reads the buffer as a flat 1D
// array and prints both the [x, y] kernel coordinate and the linear offset
// (y * W + x for DenseX). This pins:
//
//   * Index2D(W, H) means extent.X == W (columns), extent.Y == H (rows).
//   * For Stride2D.DenseX: XStride == 1, YStride == W. The X axis is contiguous
//     in memory; the linear offset of view[Index2D(x, y)] is y * W + x.
//   * The launch and the view extent agree on Index2D(W, H) — switching the
//     order (a common porting bug from C-style [row, col] code, see #1464)
//     would silently corrupt the image.
//
// Uses the 1D-buffer + As2DDenseXView reinterpret pattern (also used by the
// MemoryBufferStrides sample) so the host-side flat read-back is unambiguous
// about the linear layout.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DenseXKernel(
        Index2D index,
        ArrayView2D<int, Stride2D.DenseX> view)
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
        var view2d = buffer.View.As2DDenseXView(new Index2D(W, H));

        stream.Launch(
            new Index2D(W, H),
            index => Kernels.DenseXKernel(index, view2d));
        stream.Synchronize();

        var flat = buffer.GetAsArray1D();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                Console.WriteLine($"x={x},y={y}:{flat[y * W + x]}");
    }
}
