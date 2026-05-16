// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Allocate2DDenseXRoundTrip.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: Allocate2DDenseXRoundTrip — pins the exact #1464 user pattern.
// Goes end-to-end through the *native* 2D buffer path that the issue described:
//
//   * stream.Allocate2DDenseX<int>(new Index2D(W, H))     — native 2D buffer
//   * stream.Launch(new Index2D(W, H), …)                  — Index2D launch
//   * buffer.GetAsArray2D() → int[extent.X, extent.Y]      — host-side T[,]
//
// The DenseXRoundTrip / DenseYRoundTrip / GeneralRoundTrip / PaddedBitmap
// programs exercise the same kernel-side indexer contract via the
// Allocate1D + As2DDenseXView reinterpret pattern; this one closes the gap by
// driving the native MemoryBuffer2D allocator and the host-side
// GetAsArray2D() copy-back through the same five-backend test matrix. If the
// launcher's parameter flattening regresses for natively-allocated 2D buffers
// (the bug that LauncherStubGenerator.ExtractFieldAccessors fixed), this test
// fails first.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Allocate2DDenseXKernel(
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

        using var buffer = stream.Allocate2DDenseX<int>(new Index2D(W, H));

        stream.Launch(
            new Index2D(W, H),
            index => Kernels.Allocate2DDenseXKernel(index, buffer.View));
        stream.Synchronize();

        // Host-side T[,] — first dim is X (extent.X), second dim is Y. Iterate
        // x outer / y inner to match the layout-pinned output format.
        var arr = buffer.GetAsArray2D();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                Console.WriteLine($"x={x},y={y}:{arr[x, y]}");
    }
}
