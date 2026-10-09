// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerLoopKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Loops over pointers that compare two pointers to find their end.
/// </summary>
static unsafe class PointerLoopKernels
{
    /// <summary>
    /// Four adjacent integers.
    /// </summary>
    public struct Quad
    {
        public int A;
        public int B;
        public int C;
        public int D;
    }

    /// <summary>
    /// The sum of the fields of a local structure: a pointer moved up to an end
    /// pointer (p &lt; end).
    /// </summary>
    public static void LocalLessKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Quad q;
        q.A = data[index];
        q.B = 2;
        q.C = 3;
        q.D = 4;
        int sum = 0;
        int* end = &q.A + 4;
        for (int* p = &q.A; p < end; p++)
            sum += *p;
        data[index] = sum;
    }

    /// <summary>
    /// A pointer moved over all view elements until it equals the end pointer
    /// (p != end); the sum becomes the first element.
    /// </summary>
    public static void ViewNotEqualKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        if (index.X != 0)
            return;
        int* p = (int*)Unsafe.AsPointer(ref data[0]);
        int* end = p + data.IntLength;
        int sum = 0;
        while (p != end)
            sum += *p++;
        data[0] = sum;
    }

    /// <summary>
    /// Pointers to the neighbors of a thread in shared memory, moved towards
    /// each other while the first is below the second (p &lt; q), in groups of
    /// up to 1024 threads.
    /// </summary>
    public static void SharedLessKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(1024);
        int g = Group.Index;
        shared[g] = data[index];
        Group.Barrier();
        int* p = (int*)Unsafe.AsPointer(ref shared[g & ~3]);
        int* q = p + 3;
        int sum = 0;
        while (p < q)
            sum += *p++ - *q--;
        data[index] = sum * (g + 1);
    }

    /// <summary>
    /// A pointer into a local structure searched for the first field greater
    /// than a limit; the result is the field index or -1 (p == end).
    /// </summary>
    public static void LocalSearchKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Quad q;
        q.A = 1;
        q.B = data[index];
        q.C = data[index] - 5;
        q.D = 2;
        int* first = &q.A;
        int* end = first + 4;
        int* p = first;
        while (p != end && *p <= 20)
            ++p;
        data[index] = p == end ? -1 : (int)(p - first);
    }

    private static int SumRange(int* first, int* end)
    {
        int sum = 0;
        for (int* p = first; p < end; ++p)
            sum += *p;
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SumRangeNoInlining(int* first, int* end)
    {
        int sum = 0;
        for (int* p = first; p < end; ++p)
            sum += *p;
        return sum;
    }

    /// <summary>
    /// Ranges of a local structure and of a view element summed by helpers,
    /// inlined and not inlined.
    /// </summary>
    public static void HelperKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Quad q;
        q.A = data[index];
        q.B = 2;
        q.C = 3;
        q.D = 4;
        int* first = &q.A;
        int* e = (int*)Unsafe.AsPointer(ref data[index]);
        data[index] = SumRange(first, first + 4) +
            10 * SumRangeNoInlining(e, e + 1);
    }
}
