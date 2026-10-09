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
/// Kernels whose failure shape does NOT fit the auto-registered
/// <c>ILGPUC.Tests.Kernels</c> flow — specifically, kernels that the
/// frontend must <em>reject</em> with a clear
/// <see cref="System.NotSupportedException"/> at the intrinsic boundary
/// rather than compile. Driven directly by
/// <see cref="SharedMemoryKnownIssueTests"/>; the namespace
/// <c>ILGPUC.Tests.KnownIssues</c> is deliberately excluded by
/// <c>KernelRegistry.Discover</c> so these never feed into
/// <c>BackendTests</c>.
///
/// Positive-shape regression kernels that previously lived here have been
/// moved to <c>Kernels/SharedMemoryKernels.cs</c> (commit migrating Phase 1
/// of the sample-test extraction) so they get free coverage on all 5
/// backends via the <c>BackendTests</c> theory.
/// </summary>
static class KnownIssueKernels
{
    /// <summary>
    /// Pathological 1D variant: shared-memory extent derived from a runtime
    /// kernel parameter. Backends require a compile-time-constant extent,
    /// so the frontend must reject this shape with a clear
    /// <see cref="System.NotSupportedException"/> rather than silently
    /// crashing with an opaque internal-compiler-error.
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
    /// Pathological 2D variant: the <c>Index2D</c> extent is built from a
    /// runtime kernel parameter. The intrinsic must reject this at the
    /// frontend boundary with a <see cref="System.NotSupportedException"/>
    /// identical in shape to the 1D rejection path.
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
