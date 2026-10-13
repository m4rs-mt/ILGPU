// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerToPointer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerField PointerToPointer
// Kernels write and read pointer locals through pointers to them, also pointers
// to shared memory and view elements, and compare pointers to pointer locals
// holding pointers into different address spaces.
// Expected output: 1181 900 3201 920, 10 11 12 13, 121 231 341 451, 131 151 171 191

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    /// <summary>
    /// An address-taken pointer local written and read through a pointer to
    /// it.
    /// </summary>
    public static void PointerToPointerKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 7;
        int* q = &x;
        int** pp = &q;
        if ((index.X & 1) != 0)
            *pp = &y;
        **pp += 1;
        data[index] = x + 10 * y + 100 * *q;
    }

    /// <summary>
    /// An address-taken pointer local holding the address of shared memory, in
    /// groups of up to 1024 threads.
    /// </summary>
    public static void SharedPointerToPointerKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(1024);
        int g = Group.Index;
        shared[g] = data[index];
        Group.Barrier();
        int* q = null;
        int** pp = &q;
        *pp = (int*)Unsafe.AsPointer(ref shared[0]);
        data[index] = **pp + g;
    }

    /// <summary>
    /// An address-taken pointer local holding the address of a view element.
    /// </summary>
    public static void ViewPointerToPointerKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int* q = (int*)Unsafe.AsPointer(ref data[index]);
        int** pp = &q;
        **pp += 1;
        data[index] += 10 * **pp;
    }
    /// <summary>
    /// Pointers to two pointer locals compared with each other, where one holds
    /// the address of a local and the other the address of a view element.
    /// </summary>
    public static void MixedPointerToPointerCompareKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int* q = &x;
        int* r = (int*)Unsafe.AsPointer(ref data[index]);
        int** a = &q;
        int** b = &r;
        **a += 1;
        **b += 10;
        data[index] = (a == b ? 1000 : 0) + (a != b ? 100 : 0) + x + **b;
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
            stream.Launch((Index1D)4, index => Kernels.PointerToPointerKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.SharedPointerToPointerKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.ViewPointerToPointerKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.MixedPointerToPointerCompareKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
