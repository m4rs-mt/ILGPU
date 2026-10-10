// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GeneralRoundTrip.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: GeneralRoundTrip — pins ArrayView3D<T, Stride3D.General>
// indexing and linear-layout semantics. All three strides are user-provided
// (the Stride3D.General constructor takes an Index3D), so the launcher has
// to marshal three int slots through the nested StrideExtent auto-property.
//
// We pick strides that match a "DenseXY-equivalent" layout (XStride=1,
// YStride=W, ZStride=W*H) so the kernel-side indexer behaves identically to
// DenseXYRoundTrip. The point of this test isn't a layout the indexer has
// not seen — it's that the General wrapper threads through the same
// flattening/marshaling pipeline correctly, including the deeper nested
// StrideExtent struct.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GeneralKernel(
        Index3D index,
        ArrayView3D<int, Stride3D.General> view)
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
        var view3d = buffer.View.As3DView(
            new LongIndex3D(W, H, D),
            new Stride3D.General(new Index3D(1, W, W * H)));

        stream.Launch(
            new Index3D(W, H, D),
            index => Kernels.GeneralKernel(index, view3d));
        stream.Synchronize();

        var flat = buffer.GetAsArray1D();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                for (var z = 0; z < D; z++)
                    Console.WriteLine(
                        $"x={x},y={y},z={z}:{flat[z * W * H + y * W + x]}");
    }
}
