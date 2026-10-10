// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DenseXYRoundTrip.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DenseXYRoundTrip — pins ArrayView3D<T, Stride3D.DenseXY>
// indexing and linear-layout semantics, the 3D analogue of the #1464 #2D
// round-trip.
//
// Layout being pinned:
//   * Index3D(W, H, D) → extent.X == W, extent.Y == H, extent.Z == D.
//   * Stride3D.DenseXY: XStride == 1, YStride == W, ZStride == W * H.
//     view[Index3D(x, y, z)] resolves to linear offset z * W * H + y * W + x.
//     X is the contiguous (fast-changing) axis, Z is the slowest.
//   * Allocate1D + As3DDenseXYView(new Index3D(W, H, D)) is the layout-explicit
//     pattern: the 1D backing buffer makes the linear offset directly visible
//     so the assertion can check both the indexer and the memory order.
//
// Encoding: view[index] = index.X * 100 + index.Y * 10 + index.Z so each digit
// is identifiable from the printed value (X varies in the hundreds, Y in the
// tens, Z in the ones).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DenseXYKernel(
        Index3D index,
        ArrayView3D<int, Stride3D.DenseXY> view)
    {
        view[index] = index.X * 100 + index.Y * 10 + index.Z;
    }
}

static class Program
{
    static void Main()
    {
        const int W = 2;
        const int H = 2;
        const int D = 2;

        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(W * H * D);
        var view3d = buffer.View.As3DDenseXYView(new Index3D(W, H, D));

        stream.Launch(
            new Index3D(W, H, D),
            index => Kernels.DenseXYKernel(index, view3d));
        stream.Synchronize();

        var flat = buffer.GetAsArray1D();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                for (var z = 0; z < D; z++)
                    Console.WriteLine(
                        $"x={x},y={y},z={z}:{flat[z * W * H + y * W + x]}");
    }
}
