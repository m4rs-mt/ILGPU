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
/// Kernels that reproduce open ILGPUC codegen bugs. Intentionally placed in
/// the ILGPUC.Tests.KnownIssues namespace (not ILGPUC.Tests.Kernels) so that
/// KernelRegistry auto-discovery does NOT pick them up — they are driven by
/// dedicated [Fact(Skip=...)] tests in SharedMemoryKnownIssueTests.
/// </summary>
static class KnownIssueKernels
{
    /// <summary>
    /// Reproduces the <c>Group.GetSharedMemory&lt;T&gt;(int)</c> + 2D-index
    /// arithmetic inside a grouped <see cref="KernelIndex"/> launch bug: the
    /// frontend crashes in <c>PureValueBuilder.CreateConvert</c> on the
    /// assertion <c>targetType.BasicValueType != BasicValueType.None</c>
    /// when lowering the tile-store.
    /// </summary>
    public static void SharedMemory1DTiledKernel(
        KernelIndex index,
        ArrayView1D<float, Stride1D.Dense> data,
        int tileSize)
    {
        int local = index.GroupIndex;
        int row = local / tileSize;
        int col = local % tileSize;

        var aTile = Group.GetSharedMemory<float>(tileSize * tileSize);
        var bTile = Group.GetSharedMemory<float>(tileSize * tileSize);

        aTile[row * tileSize + col] = local;
        bTile[row * tileSize + col] = local * 2;
        Group.Barrier();

        int flat = (int)index.GridIndex * tileSize * tileSize + local;
        if (flat < data.Length)
            data[flat] =
                aTile[col * tileSize + row] + bTile[col * tileSize + row];
    }
}
