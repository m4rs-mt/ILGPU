// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Allocate3DDenseXYRoundTrip.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: Allocate3DDenseXYRoundTrip — 3D analogue of
// Allocate2DDenseXRoundTrip. Drives the native MemoryBuffer3D allocator and
// the host-side GetAsArray3D() copy-back through the same five-backend
// matrix, pinning the launcher + multi-field GetField paths for the
// natively-allocated 3D buffer surface.
//
//   * stream.Allocate3DDenseXY<int>(new Index3D(W, H, D))  — native 3D buffer
//   * stream.Launch(new Index3D(W, H, D), …)                — Index3D launch
//   * buffer.GetAsArray3D() → int[extent.X, extent.Y, extent.Z]
//
// Output matches DenseXYRoundTrip line-for-line — the difference is only
// in the host-side allocator + read-back path.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Allocate3DDenseXYKernel(
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

        using var buffer = stream.Allocate3DDenseXY<int>(new Index3D(W, H, D));

        stream.Launch(
            new Index3D(W, H, D),
            index => Kernels.Allocate3DDenseXYKernel(index, buffer.View));
        stream.Synchronize();

        var arr = buffer.GetAsArray3D();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                for (var z = 0; z < D; z++)
                    Console.WriteLine($"x={x},y={y},z={z}:{arr[x, y, z]}");
    }
}
