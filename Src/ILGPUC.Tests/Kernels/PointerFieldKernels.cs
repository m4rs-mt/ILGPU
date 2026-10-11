// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerFieldKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Pointers stored in structure fields and in address-taken pointer locals.
/// </summary>
static unsafe class PointerFieldKernels
{
    /// <summary>
    /// A pointer and a count.
    /// </summary>
    public struct Holder
    {
        public int* P;
        public int N;
    }

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
}
