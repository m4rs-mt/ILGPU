// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PaddedBitmap.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PaddedBitmap — the canonical bitmap-with-row-padding pattern
// from #1464. A row of 3 logical "pixels" stores into a buffer whose row
// stride is 5 (2 padding ints per row); the kernel iterates only the logical
// (W=3, H=2) extent, but writes through Stride2D.General(XStride=1,
// YStride=PaddedRowWidth=5) so each row's 3 stored values are followed by
// 2 untouched padding slots.
//
// Pins:
//   * The General stride layout where YStride > extent.X — i.e. there are
//     "holes" between rows that the kernel must skip over. This is exactly
//     the bug shape #1464 hit: the user allocated and indexed with mismatched
//     row strides and saw a rotated / garbled image.
//   * The launcher correctly marshals both XStride and YStride from the
//     nested Stride2D.General.StrideExtent struct (Stride.StrideExtent.X /
//     .Y in the flattened accessor list).
//
// Read-back: we print every cell of the underlying flat buffer, including the
// padding slots, which stay at zero. That makes the row-stride structure
// directly visible in the output.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PaddedKernel(
        Index2D index,
        ArrayView2D<int, Stride2D.General> view)
    {
        view[index] = index.X * 10 + index.Y + 1;
    }
}

static class Program
{
    static void Main()
    {
        const int W = 3;
        const int H = 2;
        const int PaddedRowWidth = 5;

        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(PaddedRowWidth * H);
        // Allocate1D does not zero-initialize; the padding slots between rows
        // would otherwise hold heap garbage and make the read-back below
        // non-deterministic (CI hit this — see PR #1592 build).
        buffer.View.MemSetToZero(stream);
        var view2d = buffer.View.As2DView(
            new LongIndex2D(W, H),
            new Stride2D.General(new Index2D(1, PaddedRowWidth)));

        stream.Launch(
            new Index2D(W, H),
            index => Kernels.PaddedKernel(index, view2d));
        stream.Synchronize();

        var flat = buffer.GetAsArray1D();
        for (var y = 0; y < H; y++)
            for (var col = 0; col < PaddedRowWidth; col++)
                Console.WriteLine($"y={y},col={col}:{flat[y * PaddedRowWidth + col]}");
    }
}
