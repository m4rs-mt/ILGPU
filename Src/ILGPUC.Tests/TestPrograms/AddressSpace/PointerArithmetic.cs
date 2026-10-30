// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerArithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PointerArithmetic
// Pointers to elements of int, long and 12-byte structure views are offset by
// adding and subtracting constant and variable offsets.
// With 4 threads t, ints = longs = [1, ..., 8], triples[i] = (i, 10 i, 100 i):
//   IntKernel      (t + 1) + 100 * 2 + 10000 * (4 - t)
//   LongKernel     (t + 1) + 100 * 4 + 10000 * (8 - t)
//   TripleKernel   t + 1000 * 20 + 100 * (7 - t)
// Expected output: one line per kernel.

using System;
using ILGPU;
using ILGPU.Runtime;

struct Triple
{
    public int A;
    public int B;
    public int C;
}

static unsafe class Kernels
{
    public static void IntKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        int i = index;
        fixed (int* first = &input[0])
        {
            int* p = first + i;
            int* q = p + 2;
            int* r = q - 1;
            int* s = r - i;
            int* t = first + 3 - i;
            output[i] = *p + 100 * *s + 10000 * *t;
        }
    }

    public static void LongKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> input,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        int i = index;
        fixed (long* first = &input[0])
        {
            long* p = first + i;
            long* q = p - i + 3;
            long* r = first + (7 - i);
            output[i] = *p + 100 * *q + 10000 * *r;
        }
    }

    public static void TripleKernel(
        Index1D index,
        ArrayView1D<Triple, Stride1D.Dense> input,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        int i = index;
        fixed (Triple* first = &input[0])
        {
            Triple* p = first + i;
            Triple* q = p + 2 - i;
            Triple* r = first + 7 - i;
            output[i] = p->A + 1000 * q->B + r->C;
        }
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

        var triples = new Triple[8];
        for (int i = 0; i < triples.Length; ++i)
            triples[i] = new Triple { A = i, B = 10 * i, C = 100 * i };
        using var ints = stream.Allocate1D(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        using var longs = stream.Allocate1D(new[] { 1L, 2, 3, 4, 5, 6, 7, 8 });
        using var structs = stream.Allocate1D(triples);
        using var output = stream.Allocate1D<long>(12);
        var v0 = output.View.SubView(0, 4);
        var v1 = output.View.SubView(4, 4);
        var v2 = output.View.SubView(8, 4);
        stream.Launch((Index1D)4, index => Kernels.IntKernel(index, ints.View, v0));
        stream.Launch((Index1D)4, index => Kernels.LongKernel(index, longs.View, v1));
        stream.Launch(
            (Index1D)4,
            index => Kernels.TripleKernel(index, structs.View, v2));
        stream.Synchronize();

        var result = output.GetAsArray1D();
        for (int i = 0; i < 3; i++)
            Console.WriteLine(string.Join(" ", result[(4 * i)..(4 * i + 4)]));
    }
}
