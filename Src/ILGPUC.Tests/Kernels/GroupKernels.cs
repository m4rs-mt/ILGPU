// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Tests.Framework;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for group intrinsic tests.
/// </summary>
static class GroupKernels
{
    /// <summary>
    /// Store Group.Dimension into data[index].
    /// </summary>
    public static void GroupDimensionKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Group.Dimension;
    }

    /// <summary>
    /// Group.Barrier() between write and read operations.
    /// </summary>
    public static void GroupBarrierKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = index.X;
        Group.Barrier();
        data[index] = data[index] + 1;
    }

    /// <summary>
    /// Store Group.Index into data[index].
    /// </summary>
    public static void GroupIdxKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Group.Index;
    }

    /// <summary>
    /// Group and warp predicate barriers, warp barriers and scoped fences.
    /// </summary>
    [RequiresCapability(BackendCapability.PredicateBarriers)]
    public static void PredicateBarrierKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        bool predicate = data[index] != 0;
        int groupPopCount = Group.BarrierPopCount(predicate);
        bool groupAnd = Group.BarrierAnd(predicate);
        bool groupOr = Group.BarrierOr(predicate);
        Group.MemoryFence();
        Warp.Barrier();
        int warpPopCount = Warp.BarrierPopCount(predicate);
        bool warpAnd = Warp.BarrierAnd(predicate);
        bool warpOr = Warp.BarrierOr(predicate);
        Grid.MemoryFence();
        Grid.SystemMemoryFence();
        data[index] = groupPopCount + warpPopCount +
            (groupAnd ? 1 : 0) + (groupOr ? 2 : 0) +
            (warpAnd ? 4 : 0) + (warpOr ? 8 : 0);
    }

    /// <summary>
    /// A single shared-memory element accessed through a ref local.
    /// </summary>
    public static void SharedMemoryElementRefKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        ref int shared = ref Group.GetSharedMemory<int>();
        if (Group.IsFirstThread)
            shared = data[0];
        Group.Barrier();
        data[index] = shared + index;
    }
}
