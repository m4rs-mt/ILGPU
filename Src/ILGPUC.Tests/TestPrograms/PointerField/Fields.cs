// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Fields.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerField Fields
// Kernels store pointers to locals, view elements and shared memory in structure
// fields and read and write through them, also through pointers to the
// structures and in helpers.
// Expected output: 143 253 363 473, 20 41 62 83, 21 11 41 31, 27 47 67 87, 16 26 36 46, 32 42 52 62

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

/// <summary>
/// A pointer and a count.
/// </summary>
public unsafe struct Holder
{
    public int* P;
    public int N;
}

static unsafe class Kernels
{
    /// <summary>
    /// A pointer to a local read and written through a structure field.
    /// </summary>
    public static void LocalFieldKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder h;
        h.P = &x;
        h.N = 3;
        *h.P += h.N;
        data[index] = x + 10 * *h.P;
    }

    /// <summary>
    /// A pointer to a view element written through a structure field.
    /// </summary>
    public static void ViewFieldKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Holder h;
        h.P = (int*)Unsafe.AsPointer(ref data[index]);
        h.N = index.X;
        *h.P = *h.P * 2 + h.N;
    }

    /// <summary>
    /// A pointer to shared memory read through a structure field, in groups of
    /// up to 1024 threads.
    /// </summary>
    public static void SharedFieldKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(1024);
        int g = Group.Index;
        shared[g] = data[index];
        Group.Barrier();
        Holder h;
        h.P = (int*)Unsafe.AsPointer(ref shared[g ^ 1]);
        h.N = 1;
        data[index] = *h.P + h.N;
    }

    private static int Read(Holder h) => h.P[0] + h.N;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Write(Holder h, int value) => *h.P = value + h.N;

    /// <summary>
    /// Holders of pointers to a local and to a view element passed to helpers,
    /// inlined and not inlined.
    /// </summary>
    public static void HelperFieldKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder h;
        h.P = &x;
        h.N = 5;
        Holder v;
        v.P = (int*)Unsafe.AsPointer(ref data[index]);
        v.N = 1;
        Write(v, Read(h) + Read(v));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Add(Holder h, int value) => *h.P += value * h.N;

    /// <summary>
    /// A holder of a pointer to a local passed to a helper that is not inlined.
    /// </summary>
    public static void LocalHelperFieldKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder h;
        h.P = &x;
        h.N = 2;
        Add(h, 3);
        data[index] = x;
    }

    /// <summary>
    /// A holder whose fields are written and read through a pointer to it.
    /// </summary>
    public static void HolderPointerKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder h;
        Holder* ph = &h;
        ph->P = &x;
        ph->N = 2;
        *ph->P += ph->N;
        data[index] = x + 10 * h.N;
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
            stream.Launch((Index1D)4, index => Kernels.LocalFieldKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.ViewFieldKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.SharedFieldKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.HelperFieldKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.LocalHelperFieldKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.HolderPointerKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
