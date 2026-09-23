// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FixedArray.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalArray FixedArray
// fixed (int* p = array) tests the array for null and for being empty before it
// takes the address of the first element, which failed to compile as the null
// test of an array was not supported. Thread i pins its local array in every
// iteration of a loop and writes {i*10, i*10+1, i*10+2, i*10+3} through the
// pointer, then reads slot i & 3 back through the array. Helpers pin their array
// parameters to sum them, or to return -1 for a null pointer, which they get for
// an empty array and for the unassigned (null) array of a default structure.
// FixedSelectKernel selects an array of 4 or 2 elements per thread, whose length
// is not a constant, fills it through a pinned pointer and sums it through the
// pinned pointer and through a byte pointer cast from it. A pointer pinned on
// an empty array is null, which the kernel compares with null.
// FixedVoidKernel pins such an array through a void pointer to sum it.
// Expected output: 0 11 22 33, 6 46 86 126, -2 -2 -2 -2, 606 303 1414 707,
// -7 -7 -7 -7, 6 3 14 7

using System;
using ILGPU;
using ILGPU.Runtime;

struct Holder
{
    public int[] Values;
}

static class Kernels
{
    static unsafe int SumFixed(int[] values)
    {
        int sum = 0;
        fixed (int* p = values)
        {
            for (int i = 0; i < values.Length; ++i)
                sum += p[i];
        }
        return sum;
    }

    static unsafe int FirstOrMinusOne(int[] values)
    {
        fixed (int* p = values)
            return p == null ? -1 : p[0];
    }

    static unsafe void Fill(int[] values, int start)
    {
        fixed (int* p = values)
        {
            for (int i = 0; i < values.Length; ++i)
                p[i] = start + i;
        }
    }

    static unsafe int SumLowBytes(int[] values)
    {
        int sum = 0;
        fixed (int* p = values)
        {
            byte* bytes = (byte*)p;
            for (int i = 0; i < values.Length; ++i)
                sum += bytes[i * sizeof(int)];
        }
        return sum;
    }

    public static unsafe void FixedArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] local = new int[4];
        for (int i = 0; i < 4; ++i)
        {
            fixed (int* p = local)
                p[i] = index.X * 10 + i;
        }
        Holder holder = default;
        output[index] = local[index.X & 3];
        output[index + 4] = SumFixed(local);
        output[index + 8] = FirstOrMinusOne(new int[0]) +
            FirstOrMinusOne(holder.Values);
    }

    public static unsafe void FixedSelectKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] values = (index.X & 1) == 0 ? new int[4] : new int[2];
        Fill(values, index.X);
        output[index] = SumFixed(values) * 100 + SumLowBytes(values);
        fixed (int* p = new int[0])
            output[index + 4] = p == null ? -7 : 1;
    }

    static unsafe int SumThroughVoid(int[] values)
    {
        int sum = 0;
        fixed (void* p = values)
        {
            int* ints = (int*)p;
            for (int i = 0; i < values.Length; ++i)
                sum += ints[i];
        }
        return sum;
    }

    public static void FixedVoidKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] values = (index.X & 1) == 0 ? new int[4] : new int[2];
        Fill(values, index.X);
        output[index] = SumThroughVoid(values);
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

        using var output = stream.Allocate1D<int>(12);
        stream.Launch(
            (Index1D)4,
            index => Kernels.FixedArrayKernel(index, output.View));
        using var selected = stream.Allocate1D<int>(8);
        stream.Launch(
            (Index1D)4,
            index => Kernels.FixedSelectKernel(index, selected.View));
        using var throughVoid = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.FixedVoidKernel(index, throughVoid.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in selected.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in throughVoid.GetAsArray1D())
            Console.WriteLine(v);
    }
}
