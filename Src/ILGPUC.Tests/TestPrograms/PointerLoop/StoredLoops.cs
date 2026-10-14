// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StoredLoops.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerLoop StoredLoops
// Kernels move pointers kept in structure fields and in pointer locals whose
// address is taken up to end pointers, and compare pointers to pointers.
// Expected output: 194 294 394 494, 132 232 332 432, 19 29 39 49, 131 141 151 161

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

/// <summary>
/// A pointer and a count.
/// </summary>
public unsafe struct Cursor
{
    public int* P;
    public int N;
}

/// <summary>
/// A count and a pointer that is not the first field.
/// </summary>
public unsafe struct CountedCursor
{
    public int N;
    public int* P;
}

static unsafe class Kernels
{
    /// <summary>
    /// The sum of the fields of a local structure: a pointer field of a local
    /// structure moved up to an end pointer (c.P &lt; end), counting the
    /// steps.
    /// </summary>
    public static void FieldLessKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Quad q;
        q.A = data[index];
        q.B = 2;
        q.C = 3;
        q.D = 4;
        Cursor c;
        c.P = &q.A;
        c.N = 0;
        int* end = &q.A + 4;
        int sum = 0;
        for (; c.P < end; c.P++)
        {
            sum += *c.P;
            c.N++;
        }
        data[index] = sum * 10 + c.N;
    }

    /// <summary>
    /// The sum of every second field of a local structure: a pointer in the
    /// second field of a local structure moved up to an end pointer.
    /// </summary>
    public static void TrailingFieldLessKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Quad q;
        q.A = data[index];
        q.B = 2;
        q.C = 3;
        q.D = 4;
        CountedCursor c;
        c.N = 0;
        c.P = &q.A;
        int* end = &q.A + 4;
        int sum = 0;
        for (; c.P < end; c.P += 2)
        {
            sum += *c.P;
            c.N++;
        }
        data[index] = sum * 10 + c.N;
    }

    /// <summary>
    /// The sum of the fields of a local structure: a pointer local moved up to
    /// an end pointer through a pointer to it (*pp &lt; end).
    /// </summary>
    public static void PointerToPointerLessKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Quad q;
        q.A = data[index];
        q.B = 2;
        q.C = 3;
        q.D = 4;
        int* p = &q.A;
        int** pp = &p;
        int* end = &q.A + 4;
        int sum = 0;
        while (*pp < end)
        {
            sum += **pp;
            (*pp)++;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Pointers to two pointer locals compared with each other (a == b,
    /// a != b).
    /// </summary>
    public static void PointerToPointerCompareKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 2;
        int* q = &x;
        int* r = &y;
        int** a = &q;
        int** b = &r;
        **a += 1;
        data[index] = (a == b ? 1000 : 0) + (a != b ? 100 : 0) + x + 10 * **b;
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
            stream.Launch((Index1D)4, index => Kernels.FieldLessKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.TrailingFieldLessKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.PointerToPointerLessKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.PointerToPointerCompareKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
