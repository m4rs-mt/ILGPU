// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MatrixMultiply
{
    class Program
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        static void Main()
        {
            // Performs a sanity check on the matrix multiply implementations against known inputs and output.
            var sanityMatrixA = new float[4, 3]
            {
                {  1,   2,   3 },
                {  4,   5,   6 },
                {  7,   8,   9 },
                { 10,  11,  12 },
            };

            var sanityMatrixB = new float[3, 5]
            {
                { 13, 14, 15, 16, 17 },
                { 18, 19, 20, 21, 22 },
                { 23, 24, 25, 26, 27 },
            };

            var sanityMatrixC = new float[4, 5]
            {
                { 118, 124, 130, 136, 142 },
                { 280, 295, 310, 325, 340 },
                { 442, 466, 490, 514, 538 },
                { 604, 637, 670, 703, 736 },
            };

            RunMatrixMultiply(sanityMatrixA, sanityMatrixB, sanityMatrixC);

            // Prepare random matrices
            const int m = 500;
            const int n = 500;
            const int k = 500;

            var aMatrix = CreateRandomMatrix(m, k);
            var bMatrix = CreateRandomMatrix(k, n);
            var cMatrix = MatrixMultiplyNaive(aMatrix, bMatrix);

            RunMatrixMultiply(aMatrix, bMatrix, cMatrix);
        }

        #region Helper functions

        /// <summary>
        /// Creates a matrix populated with random values.
        /// </summary>
        /// <param name="rows">The number of rows in the matrix</param>
        /// <param name="columns">The number of columns in the matrix</param>
        /// <returns>A matrix populated with random values</returns>
        [SuppressMessage(
            "Security",
            "CA5394:Do not use insecure randomness",
            Justification = "Only used for testing")]
        static float[,] CreateRandomMatrix(int rows, int columns)
        {
            var rnd = new Random();
            var matrix = new float[rows, columns];

            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < columns; j++)
                    matrix[i, j] = rnd.Next(minValue: -100, maxValue: 100);
            }

            return matrix;
        }

        /// <summary>
        /// Compares two matrices for equality.
        /// </summary>
        /// <param name="a">A dense MxN matrix</param>
        /// <param name="b">A dense MxN matrix</param>
        /// <returns>True if the matrices are equal</returns>
        static bool MatrixEqual(float[,] a, float[,] b)
        {
            var ma = a.GetLength(0);
            var na = a.GetLength(1);
            var mb = b.GetLength(0);
            var nb = b.GetLength(1);

            if (ma != mb || na != nb)
            {
                Debug.WriteLine($"Matrix dimensions do not match: [{ma}x{na}] vs [{mb}x{nb}]");
                return false;
            }

            for (var i = 0; i < ma; i++)
            {
                for (var j = 0; j < na; j++)
                {
                    var actual = a[i, j];
                    var expected = b[i, j];
                    if (actual != expected)
                    {
                        Debug.WriteLine($"Error at element location [{i}, {j}]: {actual} found, {expected} expected");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Performs matrix multiplication using each of the various implementations.
        /// </summary>
        static void RunMatrixMultiply(float[,] a, float[,] b, float[,] expectedResult)
        {
            var m = a.GetLength(0);
            var ka = a.GetLength(1);
            var kb = b.GetLength(0);
            var n = b.GetLength(1);

            Console.WriteLine($"Running matrix multiplication on [{m}x{ka}] * [{kb}x{n}]");
            var sw = new Stopwatch();

            // Naive implementation
            sw.Restart();
            var naiveResult = MatrixMultiplyNaive(a, b);
            sw.Stop();
            Debug.Assert(MatrixEqual(naiveResult, expectedResult));
            Console.WriteLine($"- Naive implementation: {sw.ElapsedMilliseconds}ms");

            // Accelerated implementations
            using var context = Context.CreateDefault();

            foreach (var device in context)
            {
                using var accelerator = device.CreateAccelerator(context);

                sw.Restart();
                var acceleratedResult = MatrixMultiplyAccelerated(accelerator, a, b);
                sw.Stop();
                Debug.Assert(MatrixEqual(acceleratedResult, expectedResult));
                Console.WriteLine($"- Accelerated implementation on {accelerator}: {sw.ElapsedMilliseconds}ms");

                sw.Restart();
                var acceleratedTiledResult = MatrixMultiplyTiled(accelerator, a, b);
                sw.Stop();
                Debug.Assert(MatrixEqual(acceleratedTiledResult, expectedResult));
                Console.WriteLine($"- Tiled implementation on {accelerator}: {sw.ElapsedMilliseconds}ms");
            }
        }

        #endregion

        #region Naive algorithm

        /// <summary>
        /// Multiplies two dense matrices and returns the resultant matrix.
        /// </summary>
        /// <param name="accelerator">The Accelerator to run the multiplication on</param>
        /// <param name="a">A dense MxK matrix</param>
        /// <param name="b">A dense KxN matrix</param>
        /// <returns>A dense MxN matrix</returns>
        static float[,] MatrixMultiplyNaive(float[,] a, float[,] b)
        {
            var m = a.GetLength(0);
            var ka = a.GetLength(1);
            var kb = b.GetLength(0);
            var n = b.GetLength(1);

            if (ka != kb)
                throw new ArgumentException($"Cannot multiply {m}x{ka} matrix by {n}x{kb} matrix", nameof(b));

            var c = new float[m, n];

            for (var x = 0; x < m; x++)
            {
                for (var y = 0; y < n; y++)
                {
                    c[x, y] = 0;

                    for (var z = 0; z < ka; z++)
                        c[x, y] += a[x, z] * b[z, y];
                }
            }

            return c;
        }

        #endregion

        #region Accelerated algorithm

        /// <summary>
        /// Multiplies two dense matrices and returns the resultant matrix.
        /// </summary>
        /// <param name="accelerator">The Accelerator to run the multiplication on</param>
        /// <param name="a">A dense MxK matrix</param>
        /// <param name="b">A dense KxN matrix</param>
        /// <returns>A dense MxN matrix</returns>
        static float[,] MatrixMultiplyAccelerated(Accelerator accelerator, float[,] a, float[,] b)
        {
            var m = a.GetLength(0);
            var ka = a.GetLength(1);
            var kb = b.GetLength(0);
            var n = b.GetLength(1);

            if (ka != kb)
                throw new ArgumentException($"Cannot multiply {m}x{ka} matrix by {n}x{kb} matrix", nameof(b));

            var kernel = accelerator.LoadAutoGroupedStreamKernel<
                Index2D,
                ArrayView2D<float, Stride2D.DenseX>,
                ArrayView2D<float, Stride2D.DenseX>,
                ArrayView2D<float, Stride2D.DenseX>>(
                MatrixMultiplyAcceleratedKernel);

            using var aBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(m, ka));
            using var bBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(ka, n));
            using var cBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(m, n));
            aBuffer.CopyFromCPU(a);
            bBuffer.CopyFromCPU(b);

            kernel(cBuffer.Extent.ToIntIndex(), aBuffer.View, bBuffer.View, cBuffer.View);

            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            return cBuffer.GetAsArray2D();
        }

        /// <summary>
        /// The matrix multiplication kernel that runs on the accelerated device.
        /// </summary>
        /// <param name="index">Current matrix index</param>
        /// <param name="aView">An input matrix of size MxK</param>
        /// <param name="bView">An input matrix of size KxN</param>
        /// <param name="cView">An output matrix of size MxN</param>
        static void MatrixMultiplyAcceleratedKernel(
            Index2D index,
            ArrayView2D<float, Stride2D.DenseX> aView,
            ArrayView2D<float, Stride2D.DenseX> bView,
            ArrayView2D<float, Stride2D.DenseX> cView)
        {
            var x = index.X;
            var y = index.Y;
            var sum = 0.0f;

            for (var i = 0; i < aView.IntExtent.Y; i++)
                sum += aView[new Index2D(x, i)] * bView[new Index2D(i, y)];

            cView[index] = sum;
        }

        #endregion

        #region Tiled algorithm

        /// <summary>
        /// Size of the tile (NxN).
        /// </summary>
        const int TILE_SIZE = 2;

        /// <summary>
        /// Multiplies two dense matrices and returns the resultant matrix (using tiling).
        /// </summary>
        /// <param name="accelerator">The Accelerator to run the multiplication on</param>
        /// <param name="a">A dense MxK matrix</param>
        /// <param name="b">A dense KxN matrix</param>
        /// <returns>A dense MxN matrix</returns>
        static float[,] MatrixMultiplyTiled(Accelerator accelerator, float[,] a, float[,] b)
        {
            var m = a.GetLength(0);
            var ka = a.GetLength(1);
            var kb = b.GetLength(0);
            var n = b.GetLength(1);

            if (ka != kb)
                throw new ArgumentException($"Cannot multiply {m}x{ka} matrix by {n}x{kb} matrix", nameof(b));

            var kernel = accelerator.LoadStreamKernel<
                ArrayView2D<float, Stride2D.DenseX>,
                ArrayView2D<float, Stride2D.DenseX>,
                ArrayView2D<float, Stride2D.DenseX>>(
                MatrixMultiplyTiledKernel);
            var groupSize = new Index2D(TILE_SIZE, TILE_SIZE);
            var numGroups = new Index2D((m + TILE_SIZE - 1) / TILE_SIZE, (n + TILE_SIZE - 1) / TILE_SIZE);

            using var aBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(m, ka));
            using var bBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(ka, n));
            using var cBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(m, n));
            aBuffer.CopyFromCPU(a);
            bBuffer.CopyFromCPU(b);

            kernel((numGroups, groupSize), aBuffer, bBuffer, cBuffer);

            // Reads data from the GPU buffer into a new CPU array.
            // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
            // that the kernel and memory copy are completed first.
            return cBuffer.GetAsArray2D();
        }

        /// <summary>
        /// The tiled matrix multiplication kernel that runs on the accelerated device.
        /// </summary>
        /// <param name="aView">An input matrix of size MxK</param>
        /// <param name="bView">An input matrix of size KxN</param>
        /// <param name="cView">An output matrix of size MxN</param>
        static void MatrixMultiplyTiledKernel(
            ArrayView2D<float, Stride2D.DenseX> aView,
            ArrayView2D<float, Stride2D.DenseX> bView,
            ArrayView2D<float, Stride2D.DenseX> cView)
        {
            var global = Grid.GlobalIndex.XY;
            var x = Group.IdxX;
            var y = Group.IdxY;

            var aTile = SharedMemory.Allocate2D<float, Stride2D.DenseX>(new Index2D(TILE_SIZE, TILE_SIZE), new Stride2D.DenseX(TILE_SIZE));
            var bTile = SharedMemory.Allocate2D<float, Stride2D.DenseX>(new Index2D(TILE_SIZE, TILE_SIZE), new Stride2D.DenseX(TILE_SIZE));
            var sum = 0.0f;

            for (var i = 0; i < aView.IntExtent.X; i += TILE_SIZE)
            {
                if (global.X < aView.IntExtent.X && y + i < aView.IntExtent.Y)
                    aTile[x, y] = aView[global.X, y + i];
                else
                    aTile[x, y] = 0;

                if (x + i < bView.IntExtent.X && global.Y < bView.IntExtent.Y)
                    bTile[x, y] = bView[x + i, global.Y];
                else
                    bTile[x, y] = 0;
                Group.Barrier();

                for (var k = 0; k < TILE_SIZE; k++)
                    sum += aTile[new Index2D(x, k)] * bTile[new Index2D(k, y)];
                Group.Barrier();
            }

            if (global.X < cView.IntExtent.X && global.Y < cView.IntExtent.Y)
                cView[global] = sum;
        }

        #endregion
    }
}
