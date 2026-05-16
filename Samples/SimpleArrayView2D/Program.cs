// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// =====================================================================================
// SimpleArrayView2D — the canonical "render into a 2D buffer" pattern.
// =====================================================================================
//
// Typical use case (e.g. an image kernel, see github.com/m4rs-mt/ILGPU/issues/1464):
// allocate a 2D buffer, launch one thread per pixel via Index2D, write through an
// ArrayView2D parameter, then copy the result back to the host as a T[,] array.
//
// Layout cheat sheet — read once and the rest of the sample is mechanical:
//
//   * `new Index2D(W, H)` builds an (X = width, Y = height) extent. ILGPU is
//     consistently X-then-Y; never (Y, X).
//   * `Allocate2DDenseX<T>(new Index2D(W, H))` allocates W * H elements with X as
//     the contiguous (fast-changing) axis: `view[Index2D(x, y)]` resolves to linear
//     offset `y * W + x`. This matches the in-memory order of a typical row-major
//     bitmap, but with the *index argument* spelled `(x, y)`, not `(y, x)`.
//   * `GetAsArray2D()` returns a `T[extent.X, extent.Y]` — i.e. `arr[x, y]`. The
//     first dimension of the returned `T[,]` is the X axis, NOT the row index.
//     That's the opposite of the everyday C# bitmap idiom (`pixels[y, x]`); pin
//     this in your head when porting CPU image-processing code.
//
// The kernel below writes a position-encoded value `x * 100 + y` into every cell;
// each digit identifies one axis, so a misindexing bug shows up immediately when
// reading the result.
// =====================================================================================

using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace SimpleArrayView2D;

static class Kernels
{
    /// <summary>
    /// Stores <c>X * 100 + Y</c> at every (x, y) of a 2D view. The position
    /// encoding makes a launch-extent vs. allocation-extent mismatch (the
    /// classic bug from #1464) immediately visible in the read-back.
    /// </summary>
    public static void RenderKernel(
        Index2D index,
        ArrayView2D<int, Stride2D.DenseX> view)
    {
        view[index] = index.X * 100 + index.Y;
    }
}

static class Program
{
    static void Main()
    {
        const int W = 6;
        const int H = 4;

        using var context = Context.CreateDefault();

        var device = context.Devices
            .OrderByDescending(d => d.AcceleratorType switch
            {
                AcceleratorType.Metal  => 5,
                AcceleratorType.Cuda   => 4,
                AcceleratorType.ROCm   => 3,
                AcceleratorType.OpenCL => 2,
                AcceleratorType.CPU    => 1,
                _                      => 0,
            })
            .First();

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        // Allocate (W = X, H = Y). The launch extent below MUST match — using
        // `new Index2D(W, H)` here and `new Index2D(H, W)` on the launch is
        // exactly the bug shape that produces a rotated/garbled image.
        using var buffer = stream.Allocate2DDenseX<int>(new Index2D(W, H));

        stream.Launch(
            new Index2D(W, H),
            index => Kernels.RenderKernel(index, buffer.View));
        stream.Synchronize();

        // GetAsArray2D() returns int[buffer.Extent.X, buffer.Extent.Y] — first
        // dimension is X, second is Y. arr[x, y] equals what the kernel wrote
        // at view[Index2D(x, y)].
        var arr = buffer.GetAsArray2D();

        Console.WriteLine($"Result is int[{arr.GetLength(0)}, {arr.GetLength(1)}]:");
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x++)
                Console.Write($"{arr[x, y],5}");
            Console.WriteLine();
        }

        // Self-check: every cell should equal the position-encoded value the
        // kernel wrote. A mismatch here is almost always a launch-vs-allocation
        // extent mix-up.
        var ok = true;
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                if (arr[x, y] != x * 100 + y)
                {
                    Console.WriteLine(
                        $"Mismatch at [{x},{y}]: got {arr[x, y]}, expected {x * 100 + y}");
                    ok = false;
                }

        Console.WriteLine(ok ? "Round-trip OK." : "Round-trip FAILED.");
        Console.WriteLine("Done.");
    }
}
