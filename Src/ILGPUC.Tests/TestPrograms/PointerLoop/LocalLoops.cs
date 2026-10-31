// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalLoops.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerLoop LocalLoops
// Kernels move pointers to local structures and arrays and to view elements up
// to or down to end pointers that they compare with, also in helpers.
// Expected output: 19 29 39 49, 14366 25516 36666 47816, 10 21 32 43, 10 21 32 43, 10 21 32 40, 100 20 30 40, -1 -1 1 1, 119 229 339 449

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

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

static unsafe class Kernels
{
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
    /// The elements of a local array, pinned with fixed, summed backwards by a
    /// pointer moved down to the first element (p &gt;= first) and forwards by
    /// a pointer moved up to the last element (p &lt;= last).
    /// </summary>
    public static void FixedArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var values = new int[4];
        for (int k = 0; k < values.Length; ++k)
            values[k] = data[index] + k;
        int sum = 0;
        fixed (int* first = values)
        {
            int* last = first + values.Length - 1;
            for (int* p = last; p >= first; --p)
                sum = 10 * sum + *p;
            for (int* p = first; p <= last; ++p)
                sum += *p;
        }
        data[index] = sum;
    }

    /// <summary>
    /// The elements of a local array, pinned with fixed, set by a pointer moved
    /// up to an end pointer and read at an index that depends on the thread.
    /// </summary>
    public static void FixedStoreKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var values = new int[4];
        fixed (int* first = values)
        {
            for (int* p = first; p < first + 4; ++p)
                *p = data[index] + (int)(p - first);
        }
        data[index] = values[index.X & 3];
    }

    /// <summary>
    /// The elements of a local array initialized with non-zero values, pinned
    /// with fixed, set by a pointer moved up to an end pointer and read at an
    /// index that depends on the thread.
    /// </summary>
    public static void FixedStoreInitKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var values = new int[4];
        values[0] = 1;
        values[1] = 2;
        values[2] = 3;
        values[3] = 4;
        fixed (int* first = values)
        {
            for (int* p = first; p < first + 4; ++p)
                *p = data[index] + (int)(p - first);
        }
        data[index] = values[index.X & 3];
    }

    /// <summary>
    /// The three elements of a local array, pinned with fixed, set by a pointer
    /// moved up to an end pointer and read at an index that depends on the
    /// thread.
    /// </summary>
    public static void FixedStoreLength3Kernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var values = new int[3];
        fixed (int* first = values)
        {
            for (int* p = first; p < first + 3; ++p)
                *p = data[index] + (int)(p - first);
        }
        data[index] = values[index.X % 3];
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

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.LocalLessKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.FixedArrayKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.FixedStoreKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.FixedStoreInitKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.FixedStoreLength3Kernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.ViewNotEqualKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.LocalSearchKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.HelperKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
