// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MatrixMultiplyKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Matrix-multiply kernels — the tiled variant exercises the grouped
/// <see cref="KernelIndex"/> + <c>Group.GetSharedMemory2D</c> +
/// <see cref="Group.Barrier"/> pattern that hit the SSA-construction +
/// CPU-launcher KernelIndex bugs fixed on <c>temp5</c>. Mirrors
/// <c>Samples/MatrixMultiply</c>.
/// </summary>
static class MatrixMultiplyKernels
{
    /// <summary>
    /// Compile-time tile size — <c>Group.GetSharedMemory2D</c> requires a
    /// constant extent.
    /// </summary>
    private const int TileSize = 2;

    /// <summary>
    /// Tiled kernel using shared memory — grouped 1D launch with manual 2D
    /// index reconstruction. Inner dot product is unrolled (TileSize == 2)
    /// to dodge a frontend SSA-construction bug noted in
    /// <c>Samples/MatrixMultiply/Program.cs</c>; preserved here so the
    /// regression test exercises the same shape as the sample.
    /// Native compilation hits the same B.2 cross-type struct-assignment
    /// bug as <c>InterleaveFields</c> — gated via <see cref="KnownFailingOnAttribute"/>.
    /// </summary>
    [KnownFailingOn(
        BackendType.Cuda, BackendType.ROCm, BackendType.OpenCL,
        Reason = "B.2 cross-type struct assignment — see Src/plans/fix_samples.md")]
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
            aTile[new Index2D(localX, localY)] =
                aView[new Index2D(globalX, localY + i)];
            bTile[new Index2D(localX, localY)] =
                bView[new Index2D(localX + i, globalY)];
            Group.Barrier();

            sum += aTile[new Index2D(localX, 0)] * bTile[new Index2D(0, localY)];
            sum += aTile[new Index2D(localX, 1)] * bTile[new Index2D(1, localY)];
            Group.Barrier();
        }

        if (globalX < cView.IntExtent.X && globalY < cView.IntExtent.Y)
            cView[new Index2D(globalX, globalY)] = sum;
    }
}
