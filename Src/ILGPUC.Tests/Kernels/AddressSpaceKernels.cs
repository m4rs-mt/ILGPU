// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AddressSpaceKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Tests.Framework;
using System.Runtime.CompilerServices;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for address space inference: references into shared,
/// local and global memory that flow through phis and helper methods.
/// </summary>
static class AddressSpaceKernels
{
    /// <summary>
    /// Increments the referenced value. Never inlined, so the reference stays a
    /// parameter whose address space depends on all callers.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Increment(ref int value) => value += 1;

    /// <summary>
    /// Returns a reference to the next element. Never inlined: the returned
    /// reference keeps the generic return type.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static ref int Next(ref int value) => ref Unsafe.Add(ref value, 1);

    /// <summary>
    /// Advances a reference through shared memory: the loop-carried reference
    /// is a phi of element addresses that all point into shared memory.
    /// </summary>
    public static void SharedPointerLoopKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
            shared[groupIdx] = groupIdx + 1;
        Group.Barrier();

        ref int current = ref shared[0];
        int sum = 0;
        for (int i = 0; i <= groupIdx; ++i)
        {
            sum += current;
            current = ref Unsafe.Add(ref current, 1);
        }
        data[index] = sum;
    }

    /// <summary>
    /// Passes a reference into shared memory to a helper method.
    /// </summary>
    public static void SharedRefHelperKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx] = index.X * 10;
            Increment(ref shared[groupIdx]);
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx];
    }

    /// <summary>
    /// Passes a reference to a local variable to a helper method.
    /// </summary>
    public static void LocalRefHelperKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X * 10;
        Increment(ref value);
        data[index] = value;
    }

    /// <summary>
    /// A reference selects one of two elements of shared memory per thread.
    /// </summary>
    public static void SharedRefSelectKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(64);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx] = 10 * (groupIdx + 1);
            shared[groupIdx + 32] = groupIdx;
        }
        Group.Barrier();

        if (groupIdx < 32)
        {
            ref int target = ref groupIdx % 2 == 0
                ? ref shared[groupIdx]
                : ref shared[groupIdx + 32];
            target += 1;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx] + shared[groupIdx + 32];
    }

    /// <summary>
    /// Selects a reference into either shared or global memory: the merged
    /// reference mixes both address spaces and must stay generic.
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void MixedSpaceRefKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
            shared[groupIdx] = 10 * (groupIdx + 1);
        data[index] = index.X;
        Group.Barrier();

        if (groupIdx < 32)
        {
            ref int target = ref groupIdx % 2 == 0
                ? ref shared[groupIdx]
                : ref data[index];
            target += 1;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] += shared[groupIdx];
    }

    /// <summary>
    /// Writes through a reference into shared memory returned by a helper
    /// method.
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void SharedRefReturnKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(33);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
        {
            shared[groupIdx + 1] = 0;
            Next(ref shared[groupIdx]) = index.X + 100;
        }
        Group.Barrier();

        if (groupIdx < 32)
            data[index] = shared[groupIdx + 1];
    }
}
