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
}
