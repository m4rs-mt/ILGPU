// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalPointerAddress.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalPointerAddress
// A local pointer into global memory has its address taken: the kernel changes the
// pointer through a pointer to it, directly and in a helper method, and reads the
// element it points to through the pointer to the pointer. A helper method stores
// a pointer into global memory in a local array and reads the element through it.
// With 4 threads: input = [1, 8, 15, 22]; even threads read input[3 - t], odd threads
// input[t] (direct); every thread reads input[t] (helper, local array)
// Expected output: 22 8 8 22 / 1 8 15 22 / 1 8 15 22

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Advance(int** pointer, int steps) => *pointer += steps;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ReadThroughLocalArray(int* pointer, int slot)
    {
        var addresses = new nint[2];
        addresses[slot] = (nint)pointer;
        return *(int*)addresses[slot];
    }

    public static void DirectKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        fixed (int* own = &input[index])
        fixed (int* mirrored = &input[3 - index])
        {
            int* p = own;
            int** pp = &p;
            if (index % 2 == 0)
                *pp = mirrored;
            data[index] = **pp;
        }
    }

    public static void HelperKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        fixed (int* first = &input[0])
        {
            int* p = first;
            Advance(&p, index);
            data[index] = *p;
        }
    }

    public static void ArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        fixed (int* element = &input[index])
            data[index] = ReadThroughLocalArray(element, index % 2);
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

        using var input = stream.Allocate1D<int>(4);
        input.View.CopyFromCPU(stream, new int[] { 1, 8, 15, 22 });
        using var buffer = stream.Allocate1D<int>(12);
        buffer.View.MemSetToZero(stream);
        var v0 = buffer.View.SubView(0, 4);
        var v1 = buffer.View.SubView(4, 4);
        var v2 = buffer.View.SubView(8, 4);
        stream.Launch(
            (Index1D)4, index => Kernels.DirectKernel(index, input.View, v0));
        stream.Launch(
            (Index1D)4, index => Kernels.HelperKernel(index, input.View, v1));
        stream.Launch(
            (Index1D)4, index => Kernels.ArrayKernel(index, input.View, v2));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0; i < 3; i++)
            Console.WriteLine(string.Join(" ", result[(4 * i)..(4 * i + 4)]));
    }
}
