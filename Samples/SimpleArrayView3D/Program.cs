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
// SimpleArrayView3D — the 3D analogue of SimpleArrayView2D.
// =====================================================================================
//
// Same shape: allocate a 3D buffer, launch one thread per (x, y, z) cell via
// Index3D, write through an ArrayView3D parameter, copy back as a T[,,] array.
//
// Layout cheat sheet:
//
//   * `new Index3D(W, H, D)` builds an (X = width, Y = height, Z = depth) extent.
//   * `Allocate3DDenseXY<T>(new Index3D(W, H, D))` makes X the contiguous axis,
//     then Y, then Z: `view[Index3D(x, y, z)]` resolves to linear offset
//     `z * W * H + y * W + x`. The orthogonal `Allocate3DDenseZY` flips the
//     contiguous axis to Z (`x * H * D + y * D + z`); the `view[Index3D(...)]`
//     indexer is layout-agnostic in either case.
//   * `GetAsArray3D()` returns a `T[extent.X, extent.Y, extent.Z]` — the first
//     dimension is X, NOT the slowest axis. `arr[x, y, z]` equals what the kernel
//     wrote at `view[Index3D(x, y, z)]` regardless of the underlying stride.
//
// The kernel encodes `x * 10000 + y * 100 + z` so each axis owns two decimal
// digits and a misindexing bug is immediately visible.
// =====================================================================================

using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace SimpleArrayView3D;

static class Kernels
{
    /// <summary>
    /// Stores a position-encoded value at every (x, y, z) of a 3D view.
    /// </summary>
    public static void RenderKernel(
        Index3D index,
        ArrayView3D<int, Stride3D.DenseXY> view)
    {
        view[index] = index.X * 10000 + index.Y * 100 + index.Z;
    }
}

static class Program
{
    static void Main()
    {
        const int W = 4;
        const int H = 3;
        const int D = 2;

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

        using var buffer = stream.Allocate3DDenseXY<int>(new Index3D(W, H, D));

        stream.Launch(
            new Index3D(W, H, D),
            index => Kernels.RenderKernel(index, buffer.View));
        stream.Synchronize();

        var arr = buffer.GetAsArray3D();

        Console.WriteLine(
            $"Result is int[{arr.GetLength(0)}, {arr.GetLength(1)}, {arr.GetLength(2)}]:");
        for (var z = 0; z < D; z++)
        {
            Console.WriteLine($"  z = {z}");
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                    Console.Write($"{arr[x, y, z],8}");
                Console.WriteLine();
            }
        }

        var ok = true;
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                for (var z = 0; z < D; z++)
                {
                    var expected = x * 10000 + y * 100 + z;
                    if (arr[x, y, z] != expected)
                    {
                        Console.WriteLine(
                            $"Mismatch at [{x},{y},{z}]: got {arr[x, y, z]}, expected {expected}");
                        ok = false;
                    }
                }

        Console.WriteLine(ok ? "Round-trip OK." : "Round-trip FAILED.");
        Console.WriteLine("Done.");
    }
}
