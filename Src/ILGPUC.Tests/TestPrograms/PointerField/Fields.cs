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
// structures, in helpers, in shared memory and in local arrays, and test a
// holder field for null.
// Expected output: 143 253 363 473, 20 41 62 83, 21 11 41 31, 27 47 67 87, 16 26 36 46, 11 8 31 8, 21 10 43 32, 11 22 33 40, 32 42 52 62, 15 25 35 45, 1516 2526 3536 4546, 9 21 29 41

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

/// <summary>
/// A pointer that may be null and a fallback value.
/// </summary>
public unsafe struct NullableHolder
{
    public int* P;
    public int Fallback;
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

    /// <summary>
    /// Holders of pointers to view elements in shared memory: every thread
    /// reads the element of its neighbor through the neighbor's holder, in
    /// groups of up to 1024 threads.
    /// </summary>
    public static void SharedHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var holders = Group.GetSharedMemory<Holder>(1024);
        int g = Group.Index;
        holders[g].P = (int*)Unsafe.AsPointer(ref data[index]);
        holders[g].N = g;
        Group.Barrier();
        int value = *holders[g ^ 1].P + holders[g ^ 1].N;
        Group.Barrier();
        data[index] = value;
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
    /// A local array of holders of pointers to view elements.
    /// </summary>
    public static void ViewHolderArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        if (index.X != 0)
            return;
        var holders = new Holder[4];
        for (int k = 0; k < holders.Length; ++k)
        {
            holders[k].P = (int*)Unsafe.AsPointer(ref data[k]);
            holders[k].N = k;
        }
        for (int k = 0; k < holders.Length; ++k)
            *holders[k].P += holders[(k + 1) % holders.Length].N;
    }

    /// <summary>
    /// A holder whose pointer is changed under a condition.
    /// </summary>
    public static void ConditionalFieldKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 7;
        Holder h;
        h.P = &x;
        h.N = 1;
        if ((index.X & 1) != 0)
            h.P = &y;
        data[index] = *h.P + h.N;
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

    /// <summary>
    /// A holder whose count is read through a byte pointer to it, which keeps it
    /// in memory.
    /// </summary>
    public static void HolderBytesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder h;
        h.P = &x;
        h.N = 5;
        *h.P += *(int*)((byte*)&h + sizeof(int*));
        data[index] = x;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadThrough(Holder h, int* p)
    {
        h.P = p;
        Add(h, 1);
        return *h.P + h.N;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadThroughLocal(Holder h, int* p)
    {
        h.P = p;
        Add(h, 1);
        return *h.P + h.N;
    }

    /// <summary>
    /// Holders of the null pointer and of a pointer to a local whose pointers
    /// are replaced by pointers to a local in helpers that are not inlined and
    /// pass them to another helper.
    /// </summary>
    public static void RetargetKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 7;
        Holder h;
        h.P = null;
        h.N = 3;
        Holder g;
        g.P = &y;
        g.N = 1;
        int first = ReadThrough(h, &x);
        data[index] = first + 100 * ReadThroughLocal(g, &x);
    }

    /// <summary>
    /// A holder of a pointer to a local that is replaced by a pointer to a local
    /// or by the null pointer under a condition and tested for null.
    /// </summary>
    public static void FieldNullCheckKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        NullableHolder holder;
        holder.P = null;
        holder.Fallback = value - 1;
        if ((index.X & 1) == 1)
            holder.P = &value;
        data[index] = holder.P != null ? *holder.P + 1 : holder.Fallback;
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
            stream.Launch((Index1D)4, index => Kernels.ConditionalFieldKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.SharedHolderKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.ViewHolderArrayKernel(index, buffer.View));
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
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.HolderBytesKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.RetargetKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.FieldNullCheckKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
