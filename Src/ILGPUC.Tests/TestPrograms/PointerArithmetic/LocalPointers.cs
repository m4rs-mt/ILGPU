// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalPointers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerArithmetic LocalPointers
// Kernels index, move and subtract pointers to the fields of local structures.
// Expected output: 49 89 129 169, 26 160 66 86, 3510 6740 9930 13180, 123 112 131 123, 192 202 212 222, 104222 33333 304222 33333

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

struct Triple
{
    public int A;
    public int B;
    public int C;
}

struct Mixed
{
    public int X;
    public long Y;
}

struct MixedPair
{
    public Mixed First;
    public Mixed Second;
}

static unsafe class Kernels
{
    /// <summary>
    /// Offsets of pointers to a local structure, including pointers converted
    /// from the address of the structure, whose element type IL does not keep.
    /// </summary>
    public static void LocalOffsetsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2 * data[index];
        t.C = 3;
        int* p = &t.A;
        data[index] = ((int*)&t)[1] + *((int*)&t + 2) + p[1] + *(p + 2) +
            *(&t.A + 2);
    }

    /// <summary>
    /// Elements at int, long, small uint and negative indices.
    /// </summary>
    public static void IndexKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2 * data[index];
        t.C = 3;
        int* p = &t.A;
        int* q = &t.C;
        int k = index.X % 3;
        long l = 2 - k;
        uint u = (uint)k;
        data[index] = p[k] + p[l] + p[u] + q[-k];
    }

    /// <summary>
    /// Pointers moved by increments, decrements and compound assignments.
    /// </summary>
    public static void MoveKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2 * data[index];
        t.C = 3;
        int* p = &t.A;
        int sum = 0;
        for (int k = 0; k < 3; ++k)
            sum += *p++;
        p -= 2;
        sum = 10 * sum + *p--;
        p += index.X & 1;
        data[index] = 10 * sum + *p;
    }

    /// <summary>
    /// An int pointer moved backward by native integers and by a long, such as
    /// p - (nint)2.
    /// </summary>
    public static void IntBackKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = 1;
        t.B = 2;
        t.C = 3;
        int* p = &t.C;
        nint k = index.X % 3;
        long l = (index.X + 1) % 3;
        data[index] = *(p - k) + 10 * *(p - l) + 100 * *(p - (nint)2);
    }

    /// <summary>
    /// Differences of int, byte and padded structure pointers.
    /// </summary>
    public static void DifferenceKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t = default;
        MixedPair pair = default;
        long elements = &t.C - &t.A;
        long bytes = (byte*)&t.C - (byte*)&t.A;
        long structures = &pair.Second - &pair.First;
        data[index] += (int)elements + 10 * (int)bytes + 100 * (int)structures;
    }

    /// <summary>
    /// Addresses moved by bytes and selected by conditions with other
    /// addresses: untyped addresses, the address of a structure cast to an
    /// int or byte pointer and moved past its first field, an int pointer
    /// moved by one byte, and an untyped address moved backward by bytes.
    /// </summary>
    public static void SelectOffsetKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2;
        t.C = 3;
        int x = 0x0400;
        int* p = &x;
        int* r = &t.C;
        bool even = (index.X & 1) == 0;
        nint a = even ? (nint)(&t.A) + 4 : (nint)(&t.C);
        int* q = even ? (int*)&t + 1 : &t.C;
        byte* b = even ? (byte*)&t + 4 : (byte*)&t.C;
        byte* c = even ? (byte*)p + 1 : (byte*)r;
        nint s = even ? (nint)(&t.B) - 4 : (nint)(&t.C);
        data[index] = *(int*)a + 10 * *q + 100 * *(int*)b + 1000 * *c +
            10000 * *(int*)s;
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
            stream.Launch((Index1D)4, index => Kernels.LocalOffsetsKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.IndexKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.MoveKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            stream.Launch((Index1D)4, index => Kernels.IntBackKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.DifferenceKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.SelectOffsetKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
