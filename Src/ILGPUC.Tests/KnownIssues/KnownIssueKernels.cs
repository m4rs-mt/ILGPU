// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KnownIssueKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.KnownIssues;

/// <summary>
/// Kernels that exercise the grouped <see cref="KernelIndex"/> + shared-memory
/// code path that was previously broken (see commit history for Bug B). Kept
/// in the <c>ILGPUC.Tests.KnownIssues</c> namespace (not
/// <c>ILGPUC.Tests.Kernels</c>) so they are NOT auto-registered by
/// <c>KernelRegistry</c> — they are driven directly by
/// <see cref="SharedMemoryKnownIssueTests"/>.
/// </summary>
static class KnownIssueKernels
{
    /// <summary>
    /// Shared-memory tile size. Must be a <c>const int</c> because backend
    /// shared-memory allocations require compile-time-constant extents.
    /// </summary>
    private const int TileSize = 4;

    /// <summary>
    /// Grouped <see cref="KernelIndex"/> kernel that reads <see cref="KernelIndex.GridIndex"/>,
    /// allocates two 1D shared-memory tiles at compile-time-constant size, and
    /// indexes them with 2D arithmetic (<c>row * TileSize + col</c>). Mirrors
    /// the tiled <c>Samples/MatrixMultiply</c> pattern after the
    /// Bug B fix.
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
    /// Pathological variant: shared-memory extent derived from a runtime
    /// kernel parameter (<c>tileSize * tileSize</c>). Backends require a
    /// compile-time-constant extent, so the frontend must reject this shape
    /// with a clear <see cref="System.NotSupportedException"/> rather than
    /// silently crashing with an opaque internal-compiler-error.
    /// </summary>
    public static void SharedMemory1DTiledKernel_RuntimeExtent(
        KernelIndex index,
        ArrayView1D<float, Stride1D.Dense> data,
        int tileSize)
    {
        int local = index.GroupIndex;
        var tile = Group.GetSharedMemory<float>(tileSize * tileSize);
        tile[local] = local;
        Group.Barrier();
        if (index.GridIndex < data.Length)
            data[(int)index.GridIndex] = tile[0];
    }

    /// <summary>
    /// 2D shared-memory tile with a compile-time-constant extent — the
    /// newobj-based <c>new Index2D(4, 4)</c> literal that the intrinsic
    /// handler must trace back through the frontend alloca/ctor/load
    /// pattern to recover the constant <c>(4, 4)</c> arguments.
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

    /// <summary>
    /// Pathological variant: the 2D extent is built from a runtime kernel
    /// parameter. The intrinsic must reject this at the frontend boundary
    /// with a <see cref="System.NotSupportedException"/> identical in shape
    /// to the 1D rejection path.
    /// </summary>
    public static void SharedMemory2DKernel_RuntimeExtent(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int tileSize)
    {
        var shared = Group.GetSharedMemory2D<int, Stride2D.DenseX>(
            new Index2D(tileSize, tileSize));
        int gi = Group.Index;
        shared[0, 0] = gi;
        Group.Barrier();
        if (index.X < data.Length)
            data[index] = shared[0, 0];
    }
}
