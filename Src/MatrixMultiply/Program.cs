// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2019 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;
using System.Diagnostics;

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
            using (var context = new Context())
            {
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
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

            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index2, ArrayView2D<float>, ArrayView2D<float>, ArrayView2D<float>>(MatrixMultiplyAcceleratedKernel);

            using (var aBuffer = accelerator.Allocate<float>(m, ka))
            using (var bBuffer = accelerator.Allocate<float>(ka, n))
            using (var cBuffer = accelerator.Allocate<float>(m, n))
            {
                aBuffer.CopyFrom(a, Index2.Zero, Index2.Zero, aBuffer.Extent);
                bBuffer.CopyFrom(b, Index2.Zero, Index2.Zero, bBuffer.Extent);

                kernel(cBuffer.Extent, aBuffer, bBuffer, cBuffer);
                accelerator.Synchronize();

                return cBuffer.GetAs2DArray();
            }
        }

        /// <summary>
        /// The matrix multiplication kernel that runs on the accelerated device.
        /// </summary>
        /// <param name="index">Current matrix index</param>
        /// <param name="aView">An input matrix of size MxK</param>
        /// <param name="bView">An input matrix of size KxN</param>
        /// <param name="cView">An output matrix of size MxN</param>
        static void MatrixMultiplyAcceleratedKernel(Index2 index, ArrayView2D<float> aView, ArrayView2D<float> bView, ArrayView2D<float> cView)
        {
            var x = index.X;
            var y = index.Y;
            var sum = 0.0f;

            for (var i = 0; i < aView.Height; i++)
                sum += aView[new Index2(x, i)] * bView[new Index2(i, y)];

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

            var kernel = accelerator.LoadStreamKernel<GroupedIndex2, ArrayView2D<float>, ArrayView2D<float>, ArrayView2D<float>>(MatrixMultiplyTiledKernel);
            var groupSize = new Index2(TILE_SIZE, TILE_SIZE);
            var numGroups = new Index2((m + TILE_SIZE - 1) / TILE_SIZE, (n + TILE_SIZE - 1) / TILE_SIZE);
            var launchDimension = new GroupedIndex2(numGroups, groupSize);

            using (var aBuffer = accelerator.Allocate<float>(m, ka))
            using (var bBuffer = accelerator.Allocate<float>(ka, n))
            using (var cBuffer = accelerator.Allocate<float>(m, n))
            {
                aBuffer.CopyFrom(a, Index2.Zero, Index2.Zero, aBuffer.Extent);
                bBuffer.CopyFrom(b, Index2.Zero, Index2.Zero, bBuffer.Extent);

                kernel(launchDimension, aBuffer, bBuffer, cBuffer);
                accelerator.Synchronize();

                return cBuffer.GetAs2DArray();
            }
        }

        /// <summary>
        /// The tiled matrix multiplication kernel that runs on the accelerated device.
        /// </summary>
        /// <param name="index">Current matrix index</param>
        /// <param name="aView">An input matrix of size MxK</param>
        /// <param name="bView">An input matrix of size KxN</param>
        /// <param name="cView">An output matrix of size MxN</param>
        static void MatrixMultiplyTiledKernel(GroupedIndex2 index, ArrayView2D<float> aView, ArrayView2D<float> bView, ArrayView2D<float> cView)
        {
            var global = index.ComputeGlobalIndex();
            var x = index.GroupIdx.X;
            var y = index.GroupIdx.Y;

            var aTile = SharedMemory.Allocate2D<float>(TILE_SIZE, TILE_SIZE);
            var bTile = SharedMemory.Allocate2D<float>(TILE_SIZE, TILE_SIZE);
            var sum = 0.0f;

            for (var i = 0; i < aView.Width; i += TILE_SIZE)
            {
                if (global.X < aView.Width && y + i < aView.Height)
                    aTile[x, y] = aView[global.X, y + i];
                else
                    aTile[x, y] = 0;

                if (x + i < bView.Width && global.Y < bView.Height)
                    bTile[x, y] = bView[x + i, global.Y];
                else
                    bTile[x, y] = 0;
                Group.Barrier();

                for (var k = 0; k < TILE_SIZE; k++)
                    sum += aTile[new Index2(x, k)] * bTile[new Index2(k, y)];
                Group.Barrier();
            }

            if (global.X < cView.Width && global.Y < cView.Height)
                cView[global] = sum;
        }

        #endregion
    }
}
