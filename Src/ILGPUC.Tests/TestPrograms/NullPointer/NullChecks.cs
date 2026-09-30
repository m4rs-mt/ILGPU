// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullChecks.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: NullPointer NullChecks
// Pointers and references that are null or point to a local, a view element or
// shared memory, merged in phis and selects, passed to helpers and tested for
// null. The null pointer had the generic address space, which Metal and OpenCL
// could not merge with or compare to pointers into other address spaces, and
// CPU kernels compared per-lane pointers as long. Each kernel runs on the
// values 10 20 30 40 and prints one line.
// Expected output:
//   11 122 132 142 (local pointer through a phi)
//   -1 21 31 41 (local pointer through a select)
//   110 20 130 40 (view element pointer)
//   11 122 31 142 (shared memory pointer)
//   1007 1060 1090 1120 (helpers testing pointer parameters)
//   110 20 130 40 (Unsafe.NullRef and Unsafe.IsNullRef)
//   -1 21 32 43 (pointer through a loop)
//   7 100 7 140 (helper testing a reference parameter)

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

struct FieldTriple
{
    public int A;
    public int B;
    public int C;
}

static class Kernels
{
    public static unsafe void LocalNullCheckKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        int* p = null;
        if (value > 15)
            p = &value;
        if (p != null)
            *p += 100;
        data[index] = value + (p == null ? 1 : 2);
    }

    public static unsafe void LocalNullSelectKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        int* p = value > 15 ? &value : null;
        data[index] = p != null ? *p + 1 : -1;
    }

    public static unsafe void ElementNullCheckKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int* p = null;
        if ((index.X & 1) == 0)
            p = (int*)Unsafe.AsPointer(ref data[index]);
        if (p != null)
            *p += 100;
    }

    public static unsafe void SharedNullCheckKernel(
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(4);
        int groupIndex = Group.Index;
        int globalIndex = (int)Grid.GlobalThreadIndex;
        shared[groupIndex] = data[globalIndex];
        Group.Barrier();

        int* p = null;
        if ((groupIndex & 1) == 1)
            p = (int*)Unsafe.AsPointer(ref shared[groupIndex]);
        if (p != null)
            *p += 100;
        Group.Barrier();
        data[globalIndex] = shared[groupIndex] + (p == null ? 1 : 2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe int ReadOrDefault(int* p, int fallback) =>
        p == null ? fallback : *p;

    static unsafe int ReadOrDefaultInlined(int* p, int fallback)
    {
        if (p != null)
            return *p;
        return fallback;
    }

    public static unsafe void HelperNullCheckKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index];
        int* local = value > 15 ? &value : null;
        int* element = (int*)Unsafe.AsPointer(ref data[index]);
        int result = ReadOrDefault(local, -1) + ReadOrDefault(null, 1000) +
            ReadOrDefaultInlined(local, -2) + ReadOrDefaultInlined(element, 0);
        data[index] = result;
    }

    public static void NullRefCheckKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        ref int r = ref Unsafe.NullRef<int>();
        if ((index.X & 1) == 0)
            r = ref data[index];
        if (!Unsafe.IsNullRef(ref r))
            r += 100;
    }

    public static unsafe void LoopNullCheckKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        FieldTriple triple;
        triple.A = data[index] + 1;
        triple.B = data[index] + 2;
        triple.C = data[index] + 3;
        int* last = null;
        for (int i = 0; i < index.X; ++i)
            last = i == 0 ? &triple.A : i == 1 ? &triple.B : &triple.C;
        data[index] = last == null ? -1 : *last;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ReadRefOrDefault(ref int r, int fallback) =>
        Unsafe.IsNullRef(ref r) ? fallback : r;

    public static void RefHelperNullCheckKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = data[index] * 2;
        data[index] = (index.X & 1) == 1
            ? ReadRefOrDefault(ref value, 0) +
                ReadRefOrDefault(ref Unsafe.NullRef<int>(), 60)
            : ReadRefOrDefault(ref Unsafe.NullRef<int>(), 7);
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
        var input = new[] { 10, 20, 30, 40 };

        using var local = stream.Allocate1D<int>(4);
        local.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index =>
            Kernels.LocalNullCheckKernel(index, local.View));

        using var select = stream.Allocate1D<int>(4);
        select.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index =>
            Kernels.LocalNullSelectKernel(index, select.View));

        using var element = stream.Allocate1D<int>(4);
        element.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index =>
            Kernels.ElementNullCheckKernel(index, element.View));

        using var shared = stream.Allocate1D<int>(4);
        shared.View.CopyFromCPU(stream, input);
        stream.Launch(new KernelConfig(1, 4), index =>
            Kernels.SharedNullCheckKernel(shared.View));

        using var helper = stream.Allocate1D<int>(4);
        helper.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index =>
            Kernels.HelperNullCheckKernel(index, helper.View));

        using var nullRef = stream.Allocate1D<int>(4);
        nullRef.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index =>
            Kernels.NullRefCheckKernel(index, nullRef.View));

        using var loop = stream.Allocate1D<int>(4);
        loop.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index =>
            Kernels.LoopNullCheckKernel(index, loop.View));

        using var refHelper = stream.Allocate1D<int>(4);
        refHelper.View.CopyFromCPU(stream, input);
        stream.Launch((Index1D)4, index =>
            Kernels.RefHelperNullCheckKernel(index, refHelper.View));
        stream.Synchronize();

        foreach (var buffer in new[]
            { local, select, element, shared, helper, nullRef, loop, refHelper })
        {
            Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
        }
    }
}
