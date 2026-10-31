// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullPointerKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for null checks of pointers and references.
/// </summary>
static class NullPointerKernels
{
    /// <summary>
    /// A pointer to a local that is null unless a condition holds, merged in a
    /// phi and tested for null twice.
    /// </summary>
    public static unsafe void LocalNullCheckKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        int* p = null;
        if (value > 15)
            p = &value;
        if (p != null)
            *p += 100;
        data[index] = value + (p == null ? 1 : 2);
    }

    /// <summary>
    /// A pointer to a local or null, selected by a condition.
    /// </summary>
    public static unsafe void LocalNullSelectKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        int* p = value > 15 ? &value : null;
        data[index] = p != null ? *p + 1 : -1;
    }

    /// <summary>
    /// A pointer to a view element or null.
    /// </summary>
    public static unsafe void ElementNullCheckKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int* p = null;
        if ((index.X & 1) == 0)
            p = (int*)Unsafe.AsPointer(ref data[index]);
        if (p != null)
            *p += 100;
    }

    /// <summary>
    /// A pointer to an element of shared memory or null (in groups of up to
    /// 1024 threads).
    /// </summary>
    public static unsafe void SharedNullCheckKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(1024);
        int groupIndex = Group.Index;
        shared[groupIndex] = data[index];
        Group.Barrier();

        int* p = null;
        if ((groupIndex & 1) == 1)
            p = (int*)Unsafe.AsPointer(ref shared[groupIndex]);
        if (p != null)
            *p += 100;
        Group.Barrier();
        data[index] = shared[groupIndex] + (p == null ? 1 : 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe int ReadOrDefault(int* p, int fallback) =>
        p == null ? fallback : *p;

    private static unsafe int ReadOrDefaultInlined(int* p, int fallback)
    {
        if (p != null)
            return *p;
        return fallback;
    }

    /// <summary>
    /// Helpers that test their pointer parameters for null, called with null,
    /// with pointers to a local and with pointers to a view element.
    /// </summary>
    public static unsafe void HelperNullCheckKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        int* local = value > 15 ? &value : null;
        int* element = (int*)Unsafe.AsPointer(ref data[index]);
        int result = ReadOrDefault(local, -1) + ReadOrDefault(null, 1000) +
            ReadOrDefaultInlined(local, -2) + ReadOrDefaultInlined(element, 0);
        data[index] = result;
    }

    /// <summary>
    /// A reference to a view element or the null reference.
    /// </summary>
    public static void NullRefCheckKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        ref int r = ref Unsafe.NullRef<int>();
        if ((index.X & 1) == 0)
            r = ref data[index];
        if (!Unsafe.IsNullRef(ref r))
            r += 100;
    }

    /// <summary>
    /// Three fields whose addresses a loop selects.
    /// </summary>
    public struct FieldTriple
    {
        public int A;
        public int B;
        public int C;
    }

    /// <summary>
    /// A pointer that is null before a loop and points to one of three fields
    /// after the iterations of thread 1 and above.
    /// </summary>
    public static unsafe void LoopNullCheckKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        FieldTriple triple;
        triple.A = data[index] + 1;
        triple.B = data[index] + 2;
        triple.C = data[index] + 3;
        int* last = null;
        for (int i = 0; i < index.X; ++i)
            last = i == 0 ? &triple.A : i == 1 ? &triple.B : &triple.C;
        data[index] = last == null ? -1 : *last;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadRefOrDefault(ref int r, int fallback) =>
        Unsafe.IsNullRef(ref r) ? fallback : r;

    /// <summary>
    /// A helper that tests its reference parameter for null, called with a
    /// reference to a local and with the null reference.
    /// </summary>
    public static void RefHelperNullCheckKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index] * 2;
        data[index] = (index.X & 1) == 1
            ? ReadRefOrDefault(ref value, 0) +
                ReadRefOrDefault(ref Unsafe.NullRef<int>(), 60)
            : ReadRefOrDefault(ref Unsafe.NullRef<int>(), 7);
    }
}
