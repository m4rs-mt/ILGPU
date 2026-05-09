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
// ArrayView2D layout cheat sheet (read this if you came here from #1464!)
// -------------------------------------------------------------------------------------
// In ILGPU, 2D extents are always built as Index2D(X, Y) — *never* (Y, X). The X axis
// is a logical "column" coordinate; the Y axis is a logical "row" coordinate. A buffer
// allocated as
//
//     using var buffer = stream.Allocate2DDenseX<T>(new Index2D(W, H));
//
// has buffer.Extent.X == W (number of columns) and buffer.Extent.Y == H (number of
// rows), and its 2D view is addressed as view[new Index2D(x, y)] — same X-then-Y
// argument order everywhere.
//
// The stride flavour decides which axis is contiguous in memory:
//
//   * Stride2D.DenseX → XStride = 1, YStride = W
//                      view[Index2D(x, y)] → element at linear offset y * W + x
//                      X varies fastest. This is column-major-by-row, equivalent to
//                      a flat row-major layout where x is the "column index" and y
//                      is the "row index" — i.e. the same memory order as a typical
//                      C bitmap or `T[Y, X]` array, but with the *index arguments*
//                      flipped: ILGPU spells it Index2D(x, y), not (y, x).
//   * Stride2D.DenseY → XStride = H, YStride = 1
//                      view[Index2D(x, y)] → element at linear offset x * H + y
//                      Y varies fastest. Useful for column-major numerics.
//
// Round-tripping to the host:
//
//     var arr = buffer.GetAsArray2D();   // returns T[buffer.Extent.X, buffer.Extent.Y]
//
// `arr` is indexed as arr[x, y] — *not* arr[y, x]. The first dimension of the returned
// `T[,]` is the X axis, matching the kernel-side view[Index2D(x, y)]. This is the
// opposite convention to the everyday C# bitmap idiom (`pixels[y, x]`), and is the
// single most common source of confusion when porting CPU image-processing code.
//
// Common pitfall (the #1464 bug): allocating with the dimensions swapped, e.g.
//
//     // WRONG: width and height transposed
//     var bad = stream.Allocate2DDenseX<byte>(new Index2D(height, width * 4));
//     stream.Launch(new Index2D(width, height), …);
//
// The kernel iterates over Index2D(width, height) but writes into a buffer whose
// Extent.X is `height` and Extent.Y is `width * 4` — every store lands in the wrong
// place and the read-back image is rotated/garbled. The correct shape is to make the
// allocation extent match the launch extent (or, for byte-per-pixel buffers,
// Index2D(width * 4, height)) so that the contiguous X axis matches your row stride.
// See `UsingBitmapStride` below for the padded-row pattern.
// =====================================================================================

using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace MemoryBufferStrides;

static class Kernels
{
    public static void Stride1DKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> denseView,
        ArrayView1D<int, Stride1D.Infinite> infiniteView,
        ArrayView1D<int, Stride1D.General> generalView)
    {
        var generalLinearIdx = generalView.Stride.ComputeElementIndex(index);
        int generalViewValue;
        if (generalLinearIdx < generalView.AsContiguous().Length)
            generalViewValue = generalView[index];
        else
            generalViewValue = -1;

        Interop.WriteLine("[{0}] = Dense= {1}, Infinite= {2}, General= {3}",
            index.X,
            denseView[index],
            infiniteView[index],
            generalViewValue);
    }

    public static void Stride2DKernel(
        Index2D index,
        ArrayView2D<int, Stride2D.DenseX> denseXView,
        ArrayView2D<int, Stride2D.DenseY> denseYView)
    {
        Interop.WriteLine("DenseX[{0}, {1}]= {2}, DenseY[{1}, {0}]= {3}",
            index.Y,
            index.X,
            denseXView[index],
            denseYView[index.Y, index.X]);
    }

    public static void BitmapStrideKernel(
        Index2D index,
        ArrayView2D<byte, Stride2D.DenseX> view)
    {
        Interop.WriteLine("DenseX[Y={0}, X={1}]= {2}",
            index.Y,
            index.X,
            view[index]);
    }
}

static class Program
{
    static void UsingStride1D(AcceleratorStream stream, Accelerator accelerator)
    {
        Console.WriteLine("Using Stride1D");
        var values = Enumerable.Range(0, 16).ToArray();

        using var denseBuffer = stream.Allocate1D(values);

        var infiniteValue = new int[] { 42 };
        using var infiniteBuffer = stream.Allocate1D<int, Stride1D.Infinite>(
            1,
            new Stride1D.Infinite());
        infiniteBuffer.View.CopyFromCPU(ref infiniteValue[0], 1);

        const int Advance = 4;
        var generalView = denseBuffer.View.AsGeneral(
            new Stride1D.General(Advance));

        stream.Launch(
            (Index1D)denseBuffer.Length,
            index => Kernels.Stride1DKernel(
                index, denseBuffer.View, infiniteBuffer.View, generalView));
        stream.Synchronize();
        Console.WriteLine();
    }

    static void UsingStride2D(AcceleratorStream stream, Accelerator accelerator)
    {
        Console.WriteLine("Using Stride2D");

        var flatValues = Enumerable.Range(0, 15).ToArray();
        using var inputBuffer = stream.Allocate1D(flatValues);

        var denseXValues = new int[,]
        {
            { 0, 1, 2, 3, 4 },
            { 5, 6, 7, 8, 9 },
            { 10, 11, 12, 13, 14 },
        };
        var dimXY = new Index2D(denseXValues.GetLength(1), denseXValues.GetLength(0));
        var denseXView = inputBuffer.View.As2DDenseXView(dimXY);

        var dimYX = new Index2D(denseXValues.GetLength(0), denseXValues.GetLength(1));
        var denseYView = inputBuffer.View.As2DDenseYView(dimYX);

        stream.Launch(
            dimXY,
            index => Kernels.Stride2DKernel(index, denseXView, denseYView));
        stream.Synchronize();

        Console.WriteLine();

        var denseYValues = new int[,]
        {
            { 0, 1, 2 },
            { 3, 4, 5 },
            { 6, 7, 8 },
            { 9, 10, 11 },
            { 12, 13, 14 },
        };
        Console.WriteLine("Using Stride2D - .NET Values");
        for (var y = 0; y < dimXY.Y; y++)
            for (var x = 0; x < dimXY.X; x++)
                Console.WriteLine(
                    $"DenseX[{y}, {x}]= {denseXValues[y, x]}, " +
                    $"DenseY[{x}, {y}]= {denseYValues[x, y]}");

        Console.WriteLine();
    }

    static void UsingBitmapStride(AcceleratorStream stream, Accelerator accelerator)
    {
        Console.WriteLine("Using Bitmap Stride");

        const int Width = 3;
        const int Height = 4;
        const int BytesPerPixel = 3;
        const int WidthInBytes = Width * BytesPerPixel;
        const int PaddedWidthInBytes = (WidthInBytes + BytesPerPixel) & ~0x3;

        using var imageBuffer = stream.Allocate2D<byte, Stride2D.DenseX>(
            new Index2D(WidthInBytes, Height),
            ex => ex.X,
            (ex, leadingDim) => new Stride2D.DenseX(PaddedWidthInBytes));

        var sourceBuffer =
            Enumerable.Range(1, (int)imageBuffer.Length)
            .Select(x => (byte)x)
            .ToArray();
        imageBuffer.View.AsContiguous().CopyFromCPU(sourceBuffer);

        stream.Launch(
            imageBuffer.IntExtent,
            index => Kernels.BitmapStrideKernel(index, imageBuffer.View));
        stream.Synchronize();
    }

    static void Main()
    {
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
        Console.WriteLine($"Performing operations on {accelerator}");
        var stream = accelerator.DefaultStream;

        UsingStride1D(stream, accelerator);
        UsingStride2D(stream, accelerator);
        UsingBitmapStride(stream, accelerator);

        Console.WriteLine("Done.");
    }
}
