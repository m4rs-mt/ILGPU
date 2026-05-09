// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DenseZYRoundTrip.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DenseZYRoundTrip — pins ArrayView3D<T, Stride3D.DenseZY>
// indexing and linear-layout semantics. Mirror of DenseXYRoundTrip with the
// orthogonal stride: Z becomes the contiguous axis.
//
// Layout being pinned:
//   * Stride3D.DenseZY: XStride == H * D, YStride == D, ZStride == 1.
//     view[Index3D(x, y, z)] resolves to linear offset x * H * D + y * D + z.
//     Z is contiguous, X is slowest.
//   * The high-level view[Index3D(x, y, z)] indexer must yield the SAME value
//     the kernel wrote at that logical coordinate as in DenseXYRoundTrip —
//     stride choice is invisible at the indexer, visible only in the linear
//     read-back.
//
// Expected output: identical to DenseXYRoundTrip per (x, y, z); only the
// underlying memory order differs.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DenseZYKernel(
        Index3D index,
        ArrayView3D<int, Stride3D.DenseZY> view)
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
        var view3d = buffer.View.As3DDenseZYView(new Index3D(W, H, D));

        stream.Launch(
            new Index3D(W, H, D),
            index => Kernels.DenseZYKernel(index, view3d));
        stream.Synchronize();

        var flat = buffer.GetAsArray1D();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                for (var z = 0; z < D; z++)
                    Console.WriteLine(
                        $"x={x},y={y},z={z}:{flat[x * H * D + y * D + z]}");
    }
}
