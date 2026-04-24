// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace MatrixMultiply;

static class Kernels
{
    // 2D accelerated kernel: called via stream.Launch(Index2D extent, ...)
    public static void MatrixMultiplyAcceleratedKernel(
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

    // NOTE: The tiled variant was removed temporarily. The previous blocker
    // (frontend crash on Group.GetSharedMemory<T> with a runtime-computed
    // extent) is now fixed — a compile-time-constant TileSize resolves it.
    // But the tiled kernel additionally hits distinct CPU-backend codegen
    // bugs (generated C# references undefined struct fields and produces
    // local name collisions). Track those separately.
}

static class Program
{
    static void Main()
    {
        // Sanity check against known inputs/output
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

        // Random matrices
        const int m = 500;
        const int n = 500;
        const int k = 500;

        var aMatrix = CreateRandomMatrix(m, k);
        var bMatrix = CreateRandomMatrix(k, n);
        var cMatrix = MatrixMultiplyNaive(aMatrix, bMatrix);

        RunMatrixMultiply(aMatrix, bMatrix, cMatrix);
    }

    #region Helper functions

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
                    Debug.WriteLine(
                        $"Error at element location [{i}, {j}]: {actual} found, {expected} expected");
                    return false;
                }
            }
        }

        return true;
    }

    static void RunMatrixMultiply(float[,] a, float[,] b, float[,] expectedResult)
    {
        var m = a.GetLength(0);
        var ka = a.GetLength(1);
        var kb = b.GetLength(0);
        var n = b.GetLength(1);

        Console.WriteLine($"Running matrix multiplication on [{m}x{ka}] * [{kb}x{n}]");
        var sw = new Stopwatch();

        // Naive implementation (CPU)
        sw.Restart();
        var naiveResult = MatrixMultiplyNaive(a, b);
        sw.Stop();
        Debug.Assert(MatrixEqual(naiveResult, expectedResult));
        Console.WriteLine($"- Naive implementation: {sw.ElapsedMilliseconds}ms");

        // Accelerated implementations
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
        var stream = accelerator.DefaultStream;

        sw.Restart();
        var acceleratedResult = MatrixMultiplyAccelerated(stream, a, b);
        sw.Stop();
        Debug.Assert(MatrixEqual(acceleratedResult, expectedResult));
        Console.WriteLine($"- Accelerated implementation on {accelerator}: {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Naive algorithm

    static float[,] MatrixMultiplyNaive(float[,] a, float[,] b)
    {
        var m = a.GetLength(0);
        var ka = a.GetLength(1);
        var kb = b.GetLength(0);
        var n = b.GetLength(1);

        if (ka != kb)
            throw new ArgumentException(
                $"Cannot multiply {m}x{ka} matrix by {n}x{kb} matrix", nameof(b));

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

    static float[,] MatrixMultiplyAccelerated(AcceleratorStream stream, float[,] a, float[,] b)
    {
        var m = a.GetLength(0);
        var ka = a.GetLength(1);
        var kb = b.GetLength(0);
        var n = b.GetLength(1);

        if (ka != kb)
            throw new ArgumentException(
                $"Cannot multiply {m}x{ka} matrix by {n}x{kb} matrix", nameof(b));

        using var aBuffer = stream.Allocate2DDenseX<float>(new Index2D(m, ka));
        using var bBuffer = stream.Allocate2DDenseX<float>(new Index2D(ka, n));
        using var cBuffer = stream.Allocate2DDenseX<float>(new Index2D(m, n));
        aBuffer.CopyFromCPU(a);
        bBuffer.CopyFromCPU(b);

        // 2D launch
        stream.Launch(
            new Index2D(m, n),
            index => Kernels.MatrixMultiplyAcceleratedKernel(
                index, aBuffer.View, bBuffer.View, cBuffer.View));
        stream.Synchronize();

        return cBuffer.GetAsArray2D();
    }

    #endregion
}
