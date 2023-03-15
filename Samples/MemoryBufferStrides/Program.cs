﻿// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;
using System.Linq;

namespace MemoryBufferStrides
{
    class Program
    {
        #region Stride1D

        static void Stride1DKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> denseView,
            ArrayView1D<int, Stride1D.Infinite> infiniteView,
            ArrayView1D<int, Stride1D.General> generalView)
        {
            // The general view could advance outside the range of the original data,
            // so check that we are within bounds.
            var generalLinearIdx = generalView.Stride.ComputeElementIndex(index);
            int generalViewValue;
            if (generalLinearIdx < generalView.AsContiguous().Length)
                generalViewValue = generalView[index];
            else
                generalViewValue = -1;

            // Show the contents of the views.
            Interop.WriteLine("[{0}] = Dense= {1}, Infinite= {2}, General= {3}",
                index.X,
                denseView[index],
                infiniteView[index],
                generalViewValue);
        }

        /// <summary>
        /// Example of using Stride1D.
        /// </summary>
        static void UsingStride1D(Accelerator accelerator)
        {
            Console.WriteLine("Using Stride1D");
            var values = Enumerable.Range(0, 16).ToArray();

            // Stride1D.Dense means that as the X index increases by 1, it will advance
            // to the next element in the array.
            using var denseBuffer = accelerator.Allocate1D(values);

            // Stride1D.Infinite means that as the X index never advances, and always
            // returns element at index 0.
            // Creates an single element array, that we use to feed all indices.
            var infiniteValue = new int[] { 42 };
            using var infiniteBuffer = accelerator.Allocate1D<int, Stride1D.Infinite>(
                1,
                new Stride1D.Infinite());
            infiniteBuffer.View.CopyFromCPU(ref infiniteValue[0], 1);

            // Stride1D.General allows for a user-defined striding. For this example, we
            // allocate a number of elements, and then tell the view to advance X
            // elements at a time.
            const int Advance = 4;
            var generalView = denseBuffer.View.AsGeneral(
                new Stride1D.General(Advance));

            var kernel = accelerator.LoadAutoGroupedStreamKernel<
                Index1D,
                ArrayView1D<int, Stride1D.Dense>,
                ArrayView1D<int, Stride1D.Infinite>,
                ArrayView1D<int, Stride1D.General>>(
                Stride1DKernel);
            kernel(
                (int)denseBuffer.Length,
                denseBuffer.View,
                infiniteBuffer.View,
                generalView);

            accelerator.Synchronize();
            Console.WriteLine();
        }

        #endregion

        #region Stride2D

        static void Stride2DKernel(
            Index2D index,
            ArrayView2D<int, Stride2D.DenseX> denseXView,
            ArrayView2D<int, Stride2D.DenseY> denseYView)
        {
            Interop.WriteLine("DenseX[{0}, {1}]= {2}, DenseY[{1}, {0}]= {3}",
                index.Y,
                index.X,
                denseXView[index], // denseXView[index.X, index.Y],
                denseYView[index.Y, index.X]);
        }

        /// <summary>
        /// Example of using Stride2D.DenseX and Stride2D.DenseY.
        /// </summary>
        static void UsingStride2D(Accelerator accelerator)
        {
            Console.WriteLine("Using Stride2D");

            // Prepare a flat list of values, and re-interpret the values as either
            // Stride2D.DenseX or Stride2D.DenseY, in order to show the difference.
            var flatValues = Enumerable.Range(0, 15).ToArray();
            using var inputBuffer = accelerator.Allocate1D(flatValues);

            // Stride2D.DenseX indicates that to get to X + 1 you add 1 because that is
            // the "most dense" dimension. The "next dense" dimension is Y, so to get to
            // Y + 1, you have to skip over all the all X values.
            //
            // xStride | -|
            // yStride | -------------|
            //         X0 X1 X2 X3 X4 X0 X1 X2 X3 X4 X0 X1 X2 X3 X4 X5
            //         Y0             Y1             Y2
            //
            // This is also known as row-major order, and is the standard layout of .NET
            // arrays.
            //
            // NB: The value 8 in .NET array below, is accessed by "array[1,3]". The
            // value 9 is accessed by "array[1,4]". And the value 12 by "array[2,2].
            // For .NET multi-dimensional arrays, this is equivalent to "array[y,x]".
            // This is because the contiguous memory layout of the array keeps the values
            // 8 and 9 next to each other, and the value 12 is X+4 away from the value 8.
            //
            var denseXValues = new int[,]
            {                           // Row Major                      |---> X-axis
                { 0, 1, 2, 3, 4 },      //  --> --> --> --> -->           |
                { 5, 6, 7, 8, 9 },      //  --> --> --> --> -->           |
                { 10, 11, 12, 13, 14 }, //  --> --> --> --> -->           v
            };                          //                              Y-axis
            var dimXY = new Index2D(denseXValues.GetLength(1), denseXValues.GetLength(0));
            var denseXView = inputBuffer.View.As2DDenseXView(dimXY);

            // Stride2D.DenseY indicates that to get to Y + 1 you add 1 because that is
            // the "most dense" dimension. The "next dense" dimension is X, so to get to
            // X + 1, you have to skip over all the all Y values.
            //
            // xStride | -------------|
            // yStride | -|
            //         X0             X1             X2
            //         Y0 Y1 Y2 Y3 Y4 Y0 Y1 Y2 Y3 Y4 Y0 Y1 Y2 Y3 Y4
            //
            // This is also known as column-major order.
            var dimYX = new Index2D(denseXValues.GetLength(0), denseXValues.GetLength(1));
            var denseYView = inputBuffer.View.As2DDenseYView(dimYX);

            var kernel = accelerator.LoadAutoGroupedStreamKernel<
                Index2D,
                ArrayView2D<int, Stride2D.DenseX>,
                ArrayView2D<int, Stride2D.DenseY>>(
                Stride2DKernel);

            kernel(dimXY, denseXView, denseYView);
            accelerator.Synchronize();

            Console.WriteLine();

            // .NET 2D arrays use the row-major order layout, matching Stride2D.DenseX.
            //
            // For row-major order, we expect the output for each (Y, X) index to match
            // what we would get from a .NET array.
            //
            //  var denseXValues = new int[,]
            //  {                           // Row Major                  |---> X-axis
            //      { 0, 1, 2, 3, 4 },      //  --> --> --> --> -->       |
            //      { 5, 6, 7, 8, 9 },      //  --> --> --> --> -->       |
            //      { 10, 11, 12, 13, 14 }, //  --> --> --> --> -->       v
            //  };                          //                          Y-axis
            //
            // Since Stride2D.DenseY is column-major order, we instead expect each (X, Y)
            // index to match what we would get from a .NET array.
            //
            var denseYValues = new int[,]
            {                   // Column Major             |---> X-axis
                { 0, 1, 2 },    //  |    /|    /|           |
                { 3, 4, 5 },    //  |   / |   / |           |
                { 6, 7, 8 },    //  |  /  |  /  |           |
                { 9, 10, 11 },  //  | /   | /   |           |
                { 12, 13, 14 }, //  |/    |/    |           v
            };                  //                        Y-axis
            Console.WriteLine("Using Stride2D - .NET Values");
            for (var y = 0; y < dimXY.Y; y++)
                for (var x = 0; x < dimXY.X; x++)
                    Console.WriteLine(
                        $"DenseX[{y}, {x}]= {denseXValues[y, x]}, " +
                        $"DenseY[{x}, {y}]= {denseYValues[x, y]}");

            Console.WriteLine();
        }

        #endregion

        #region Bitmap Stride

        static void BitmapStrideKernel(
            Index2D index,
            ArrayView2D<byte, Stride2D.DenseX> view)
        {
            Interop.WriteLine("DenseX[Y={0}, X={1}]= {2}",
                index.Y,
                index.X,
                view[index]);
        }

        /// <summary>
        /// Example of using a 24-bit bitmap as an example of Stride2D.DenseX.
        /// </summary>
        static void UsingBitmapStride(Accelerator accelerator)
        {
            Console.WriteLine("Using Bitmap Stride");

            // A 24-bit bitmap with RGB pixels requires 3 bytes per pixel. The bitmap
            // format requires that each row is padded to a multiple of 4 bytes. For a
            // bitmap of width=3 and height=3, each row is 9 bytes (3 bytes per pixel),
            // with 3 bytes of padding, for a total buffer size of 45 bytes.
            //
            // The memory layout is:
            //
            //   X ->  0  1  2  3  4  5  6  7  8  9 10 11
            // Y
            // |  0    R  G  B  R  G  B  R  G  B  p  p  p
            // |  1    R  G  B  R  G  B  R  G  B  p  p  p
            // |  2    R  G  B  R  G  B  R  G  B  p  p  p
            // v  3    R  G  B  R  G  B  R  G  B
            //
            // Reference: https://en.wikipedia.org/wiki/BMP_file_format
            //
            const int Width = 3;
            const int Height = 4;
            const int BytesPerPixel = 3;
            const int WidthInBytes = Width * BytesPerPixel;
            const int PaddedWidthInBytes = (WidthInBytes + BytesPerPixel) & ~0x3;

            using var imageBuffer = accelerator.Allocate2D<byte, Stride2D.DenseX>(
                new Index2D(WidthInBytes, Height),
                ex => ex.X,
                (ex, leadingDim) => new Stride2D.DenseX(PaddedWidthInBytes));

            // Populate the image buffer from a flat array, including the padding.
            //
            // var sourceBuffer =                              PADDING
            //     new byte[Height, PaddedWidthInBytes]       |   |   |
            //     {                                          v   v   v 
            //         {  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12 },
            //         { 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 },
            //         { 25, 26, 27, 28, 20, 30, 31, 32, 33, 34, 35, 36 },
            //         { 37, 38, 39, 40, 41, 42, 43, 44, 45 }
            //     };
            var sourceBuffer =
                Enumerable.Range(1, (int)imageBuffer.Length)
                .Select(x => (byte)x)
                .ToArray();
            imageBuffer.View.AsContiguous().CopyFromCPU(sourceBuffer);

            // Iterate over the pixels of the image, skipping the padding.
            var kernel = accelerator.LoadAutoGroupedStreamKernel<
                Index2D,
                ArrayView2D<byte, Stride2D.DenseX>>(
                    BitmapStrideKernel);
            kernel(imageBuffer.IntExtent, imageBuffer.View);
            accelerator.Synchronize();
        }

        #endregion

        /// <summary>
        /// Demonstrates memory buffer and array view striding.
        /// </summary>
        static void Main()
        {
            // Create main context
            using var context = Context.CreateDefault();

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                UsingStride1D(accelerator);
                UsingStride2D(accelerator);
                UsingBitmapStride(accelerator);
            }
        }
    }
}
