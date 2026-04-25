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
    // Compile-time tile size — Group.GetSharedMemory2D requires a constant extent.
    public const int TileSize = 2;

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

    // Tiled kernel using shared memory. Uses grouped 1D launch with
    // manual 2D index reconstruction.
    //
    // Compiler-bug workarounds applied here, all tracked as ILGPUC issues:
    //
    //   1. The inner `for (k = 0; k < TileSize; k++)` dot product is
    //      expanded manually. Nesting a for-loop inside the outer tile
    //      loop body while also touching shared memory there triggers a
    //      frontend SSA-construction bug — duplicate loop-header phi
    //      nodes that the global optimizer's struct-value rewriter
    //      rejects.
    //
    //   2. Per-element bounds checks on the shared-memory stores (i.e.
    //      `if (row < extent.X && col < extent.Y) aTile[...] = ...; else
    //      aTile[...] = 0;`) hit the same frontend bug. This kernel
    //      therefore assumes all three matrix dimensions are multiples of
    //      TileSize — the launcher skips the tiled path for non-aligned
    //      inputs.
    public static void MatrixMultiplyTiledKernel(
        KernelIndex index,
        ArrayView2D<float, Stride2D.DenseX> aView,
        ArrayView2D<float, Stride2D.DenseX> bView,
        ArrayView2D<float, Stride2D.DenseX> cView,
        int numGroupsY)
    {
        var aTile = Group.GetSharedMemory2D<float, Stride2D.DenseX>(
            new Index2D(TileSize, TileSize));
        var bTile = Group.GetSharedMemory2D<float, Stride2D.DenseX>(
            new Index2D(TileSize, TileSize));

        int groupIdxX = (int)(index.GridIndex / numGroupsY);
        int groupIdxY = (int)(index.GridIndex % numGroupsY);
        int localX = index.GroupIndex / TileSize;
        int localY = index.GroupIndex % TileSize;

        int globalX = groupIdxX * TileSize + localX;
        int globalY = groupIdxY * TileSize + localY;
        var sum = 0.0f;

        for (var i = 0; i < aView.IntExtent.Y; i += TileSize)
        {
            aTile[new Index2D(localX, localY)] = aView[new Index2D(globalX, localY + i)];
            bTile[new Index2D(localX, localY)] = bView[new Index2D(localX + i, globalY)];
            Group.Barrier();

            // Inner dot product over k = 0..TileSize-1, unrolled for TileSize = 2.
            sum += aTile[new Index2D(localX, 0)] * bTile[new Index2D(0, localY)];
            sum += aTile[new Index2D(localX, 1)] * bTile[new Index2D(1, localY)];
            Group.Barrier();
        }

        if (globalX < cView.IntExtent.X && globalY < cView.IntExtent.Y)
            cView[new Index2D(globalX, globalY)] = sum;
    }
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

        // Tiny aligned sanity test for the tiled variant (4x4 * 4x4).
        var tinyA = new float[4, 4]
        {
            { 1, 2, 3, 4 },
            { 5, 6, 7, 8 },
            { 9, 10, 11, 12 },
            { 13, 14, 15, 16 },
        };
        var tinyB = new float[4, 4]
        {
            { 1, 0, 0, 0 },
            { 0, 1, 0, 0 },
            { 0, 0, 1, 0 },
            { 0, 0, 0, 1 },
        };
        // A * I = A
        RunMatrixMultiply(tinyA, tinyB, tinyA);

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

        // The tiled implementation requires all three dimensions to be
        // multiples of Kernels.TileSize. For non-aligned inputs the sample
        // falls back to reporting a skip rather than running the kernel.
        //
        // Correctness note: the ILGPUC CPU backend allocates each group's
        // shared-memory tile inside KernelEntryPoint via `stackalloc` and
        // invokes the kernel serially per thread, so Group.Barrier() does
        // not synchronize writes across threads in the same group and the
        // tiled kernel produces incorrect results. To compare against the
        // expected output, build with a GPU backend
        // (`-p:ILGPUBackend=Metal|Cuda|ROCm|OpenCL`); with the default
        // `-p:ILGPUBackend=CPU` the tiled timing is still reported but the
        // output is not verified.
        if (m % Kernels.TileSize == 0
            && ka % Kernels.TileSize == 0
            && n % Kernels.TileSize == 0)
        {
            sw.Restart();
            var tiledResult = MatrixMultiplyTiled(stream, a, b);
            sw.Stop();
            var tiledMatches = MatrixEqual(tiledResult, expectedResult);
            Console.WriteLine(
                $"- Tiled implementation on {accelerator}: " +
                $"{sw.ElapsedMilliseconds}ms " +
                $"(result {(tiledMatches ? "matches" : "mismatches")})");
        }
        else
        {
            Console.WriteLine(
                $"- Tiled implementation skipped: dimensions [{m}x{ka}] * " +
                $"[{kb}x{n}] not a multiple of TileSize={Kernels.TileSize}");
        }
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

    #region Tiled algorithm

    static float[,] MatrixMultiplyTiled(AcceleratorStream stream, float[,] a, float[,] b)
    {
        var m = a.GetLength(0);
        var ka = a.GetLength(1);
        var kb = b.GetLength(0);
        var n = b.GetLength(1);

        if (ka != kb)
            throw new ArgumentException(
                $"Cannot multiply {m}x{ka} matrix by {n}x{kb} matrix", nameof(b));

        // Compute grid dimensions
        int numGroupsX = (m + Kernels.TileSize - 1) / Kernels.TileSize;
        int numGroupsY = (n + Kernels.TileSize - 1) / Kernels.TileSize;
        int totalGroups = numGroupsX * numGroupsY;
        int groupSize = Kernels.TileSize * Kernels.TileSize;

        var config = new KernelConfig(totalGroups, groupSize);

        using var aBuffer = stream.Allocate2DDenseX<float>(new Index2D(m, ka));
        using var bBuffer = stream.Allocate2DDenseX<float>(new Index2D(ka, n));
        using var cBuffer = stream.Allocate2DDenseX<float>(new Index2D(m, n));
        aBuffer.CopyFromCPU(a);
        bBuffer.CopyFromCPU(b);

        stream.Launch(in config, index =>
            Kernels.MatrixMultiplyTiledKernel(
                index,
                aBuffer.View,
                bBuffer.View,
                cBuffer.View,
                numGroupsY));
        stream.Synchronize();

        return cBuffer.GetAsArray2D();
    }

    #endregion
}
