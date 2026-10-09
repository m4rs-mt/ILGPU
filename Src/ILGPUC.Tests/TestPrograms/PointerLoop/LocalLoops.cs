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
// Kernels move pointers to local structures and to view elements up to end
// pointers that they compare with, also in helpers.
// Expected output: 19 29 39 49, 100 20 30 40, -1 -1 1 1, 119 229 339 449

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
