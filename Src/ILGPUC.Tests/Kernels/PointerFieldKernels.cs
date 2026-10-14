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
using ILGPUC.Tests.Framework;
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
    /// A count and a pointer that is not the first field.
    /// </summary>
    public struct CountedHolder
    {
        public int N;
        public int* P;
    }

    /// <summary>
    /// A pointer that may be null and a fallback value.
    /// </summary>
    public struct NullableHolder
    {
        public int* P;
        public int Fallback;
    }

    /// <summary>
    /// A pointer incremented by an instance method.
    /// </summary>
    public struct Counter
    {
        public int* P;

        public void Increment() => *P += 1;
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadHolder(Holder h) => *h.P + h.N;

    /// <summary>
    /// A holder of a pointer to a local or of a pointer to a view element,
    /// selected by a condition and passed to a helper that is not inlined (CPU,
    /// CUDA and ROCm only: Metal and OpenCL reject pointers that can point to
    /// global and local memory).
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void SelectHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder a;
        a.P = &x;
        a.N = 1;
        Holder b;
        b.P = (int*)Unsafe.AsPointer(ref data[index]);
        b.N = 2;
        data[index] = 10 * ReadHolder((index.X & 1) == 0 ? a : b);
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

    /// <summary>
    /// Holders of pointers to two locals, one of which is selected in a loop
    /// and assigned to the first: the first holder is updated with values
    /// derived from its own value.
    /// </summary>
    public static void SelfUpdatingHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 5;
        Holder a;
        a.P = &x;
        a.N = 1;
        Holder b;
        b.P = &y;
        b.N = 2;
        int sum = 0;
        for (int k = 0; k < 3; ++k)
        {
            Holder c = ((k + index.X) & 1) == 0 ? a : b;
            sum += *c.P * c.N;
            a = c;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Holders with a pointer to a local in their second field, one of which
    /// is selected in a loop and assigned to the first.
    /// </summary>
    public static void SelfUpdatingCountedKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 5;
        CountedHolder a;
        a.N = 1;
        a.P = &x;
        CountedHolder b;
        b.N = 2;
        b.P = &y;
        int sum = 0;
        for (int k = 0; k < 3; ++k)
        {
            CountedHolder c = ((k + index.X) & 1) == 0 ? a : b;
            sum += *c.P * c.N;
            a = c;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Structures holding a single pointer to a local, one of which is selected
    /// in a loop, incremented through an instance method and assigned to the
    /// first.
    /// </summary>
    public static void SelfUpdatingCounterKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 0;
        Counter a;
        a.P = &x;
        Counter b;
        b.P = &y;
        for (int k = 0; k < 4; ++k)
        {
            Counter c = (k & 1) == 0 ? a : b;
            c.Increment();
            a = c;
        }
        data[index] = x + 100 * y;
    }

    /// <summary>
    /// A holder loaded from a view of holders or a holder of a pointer to a
    /// local, selected by a condition (CPU, CUDA and ROCm only: Metal and OpenCL
    /// reject structure fields that hold generic pointers to local memory).
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void LoadedHolderKernel(
        Index1D index,
        ArrayView1D<Holder, Stride1D.Dense> holders,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder local;
        local.P = &x;
        local.N = 1;
        Holder h = (index.X & 1) == 0 ? holders[index] : local;
        data[index] = *h.P + h.N;
    }

    /// <summary>
    /// A pointer to a local replaced by a pointer loaded from a view of holders
    /// under a condition (CPU, CUDA and ROCm only: Metal and OpenCL reject
    /// pointers that can point to global and local memory).
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void LoadedPointerKernel(
        Index1D index,
        ArrayView1D<Holder, Stride1D.Dense> holders,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int* p = &x;
        if ((index.X & 1) == 0)
            p = holders[index].P;
        data[index] = *p;
    }

    /// <summary>
    /// Holders of a pointer to a local and of a pointer to a view element
    /// swapped in a loop (CPU, CUDA and ROCm only: Metal and OpenCL reject
    /// pointers to local memory held in structure fields that hold generic
    /// pointers).
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void SwapHoldersKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = 1;
        Holder a;
        a.P = &x;
        a.N = 1;
        Holder b;
        b.P = (int*)Unsafe.AsPointer(ref data[index]);
        b.N = 2;
        for (int k = 0; k < 3; ++k)
        {
            *a.P += a.N;
            Holder t = a;
            a = b;
            b = t;
        }
        data[index] += 100 * x;
    }

    /// <summary>
    /// Structures holding a single pointer to a local, one of which is selected
    /// in a loop, incremented through an instance method and assigned to the
    /// first, which is given a pointer to a view element after the loop (CPU,
    /// CUDA and ROCm only: at O0, Metal and OpenCL reject pointers to local
    /// memory stored in memory that holds generic pointers).
    /// </summary>
    [RequiresCapability(BackendCapability.GenericAddressSpace)]
    public static void LaterGlobalCounterKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 0;
        Counter a;
        a.P = &x;
        Counter b;
        b.P = &y;
        for (int k = 0; k < 4; ++k)
        {
            Counter c = (k & 1) == 0 ? a : b;
            c.Increment();
            a = c;
        }
        int sum = x + 100 * y;
        a.P = (int*)Unsafe.AsPointer(ref data[index]);
        a.Increment();
        data[index] += sum;
    }

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
