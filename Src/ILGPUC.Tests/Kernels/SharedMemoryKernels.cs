// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for shared memory tests.
/// </summary>
static class SharedMemoryKernels
{
    private const int TileSize = 4;

    /// <summary>
    /// Allocate shared memory, write to it, barrier, read from it.
    /// </summary>
    public static void SharedMemoryVariableKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;

        if (groupIdx < 32)
            shared[groupIdx] = groupIdx * 2;
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx];
    }

    /// <summary>
    /// Shared array with group synchronization.
    /// Each thread writes its value, barrier, then reads neighbor's value.
    /// </summary>
    public static void SharedMemoryArrayKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(64);
        int groupIdx = Group.Index;
        int groupDim = Group.Dimension;

        if (groupIdx < 64)
            shared[groupIdx] = index.X;
        Group.Barrier();

        // Read the next element (wrapping around within the shared block)
        if (groupIdx < 64)
        {
            int next = (groupIdx + 1) % groupDim;
            if (next < 64)
                data[index] = shared[next];
            else
                data[index] = shared[groupIdx];
        }
    }

    /// <summary>
    /// Grouped <see cref="KernelIndex"/> kernel that allocates two 1D shared
    /// tiles at a compile-time-constant extent and indexes them with 2D
    /// arithmetic. Mirrors the tiled <c>Samples/MatrixMultiply</c> shape
    /// post-fix; previously broken via the grouped <c>KernelIndex</c> +
    /// shared-memory code path.
    /// </summary>
    public static void SharedMemory1DTiledKernel(
        KernelIndex index,
        ArrayView1D<float, Stride1D.Dense> data)
    {
        int local = index.GroupIndex;
        int row = local / TileSize;
        int col = local % TileSize;

        var aTile = Group.GetSharedMemory<float>(TileSize * TileSize);
        var bTile = Group.GetSharedMemory<float>(TileSize * TileSize);

        aTile[row * TileSize + col] = local;
        bTile[row * TileSize + col] = local * 2;
        Group.Barrier();

        int flat = (int)index.GridIndex * TileSize * TileSize + local;
        if (flat < data.Length)
            data[flat] =
                aTile[col * TileSize + row] + bTile[col * TileSize + row];
    }

    /// <summary>
    /// 2D shared-memory tile with a compile-time-constant extent built from a
    /// <c>new Index2D(N, N)</c> literal — exercises the frontend
    /// alloca/ctor/load pattern that materialises the constant
    /// <c>(N, N)</c> arguments for the <c>Group.GetSharedMemory2D</c>
    /// intrinsic.
    /// </summary>
    public static void SharedMemory2DKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory2D<int, Stride2D.DenseX>(
            new Index2D(TileSize, TileSize));
        int gi = Group.Index;
        if (gi < TileSize * TileSize)
            shared[gi / TileSize, gi % TileSize] = gi;
        Group.Barrier();
        if (index.X < data.Length && gi < TileSize * TileSize)
            data[index] = shared[gi % TileSize, gi / TileSize];
    }
}
