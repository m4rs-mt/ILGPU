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
// Test program: GeneralRoundTrip — pins ArrayView2D<T, Stride2D.General>
// indexing and linear-layout semantics. The General stride is the catch-all
// flavour: both XStride and YStride are user-provided, so the launcher must
// marshal both as separate slots (DenseX / DenseY each have one constant
// stride field that drops out of the IR).
//
// We use XStride == 1 and YStride == W so the layout is identical to DenseX,
// which makes the expected output match the DenseX round-trip line-for-line —
// the only thing exercised here is the launcher's ability to thread both
// stride slots through the General wrapper (and through Stride2D.General's
// nested StrideExtent auto-property).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GeneralKernel(
        Index2D index,
        ArrayView2D<int, Stride2D.General> view)
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
        var view2d = buffer.View.As2DView(
            new LongIndex2D(W, H),
            new Stride2D.General(new Index2D(1, W)));

        stream.Launch(
            new Index2D(W, H),
            index => Kernels.GeneralKernel(index, view2d));
        stream.Synchronize();

        var flat = buffer.GetAsArray1D();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                Console.WriteLine($"x={x},y={y}:{flat[y * W + x]}");
    }
}
