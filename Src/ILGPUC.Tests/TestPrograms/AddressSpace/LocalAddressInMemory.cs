// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalAddressInMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalAddressInMemory
// Addresses of local variables and of shared memory are stored to memory and the
// variables are read back through them: the address of a local variable in a local
// pointer whose address is taken, in a view of nint addresses, and the address of an
// element of shared memory in a view of structs holding a pointer.
// With 4 threads: data = [1, 8, 15, 22]; even threads read 2 * data[t] + 1, odd threads
// data[t] + 1 (local pointer); data[t] + 1 (view of nint); t + 2 (shared memory)
// Expected output: 3 9 31 23 / 2 9 16 23 / 2 3 4 5

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct TaggedPointer
{
    public int* P;
    public int Tag;
}

static unsafe class Kernels
{
    public static void LocalPointerKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 2 * x;
        int* p = &x;
        int** pp = &p;
        if (index % 2 == 0)
            *pp = &y;
        data[index] = **pp + 1;
    }

    public static void AddressViewKernel(
        Index1D index,
        ArrayView1D<nint, Stride1D.Dense> addresses,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index] + 1;
        addresses[index] = (nint)(&x);
        data[index] = *(int*)addresses[index];
    }

    public static void SharedAddressKernel(
        Index1D index,
        ArrayView1D<TaggedPointer, Stride1D.Dense> pointers,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int groupIdx = Group.Index;
        if (groupIdx < 32)
            shared[groupIdx] = index.X + 1;
        Group.Barrier();

        if (groupIdx < 32)
        {
            fixed (int* p = &shared[groupIdx])
                pointers[index] = new TaggedPointer { P = p, Tag = 1 };
            data[index] = *pointers[index].P + pointers[index].Tag;
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

        var input = new int[] { 1, 8, 15, 22 };
        using var buffer = stream.Allocate1D<int>(12);
        var v0 = buffer.View.SubView(0, 4);
        var v1 = buffer.View.SubView(4, 4);
        var v2 = buffer.View.SubView(8, 4);
        v0.CopyFromCPU(stream, input);
        v1.CopyFromCPU(stream, input);
        v2.MemSetToZero(stream);
        using var addresses = stream.Allocate1D<nint>(4);
        using var pointers = stream.Allocate1D<TaggedPointer>(4);
        stream.Launch((Index1D)4, index => Kernels.LocalPointerKernel(index, v0));
        stream.Launch(
            (Index1D)4,
            index => Kernels.AddressViewKernel(index, addresses.View, v1));
        stream.Launch(
            (Index1D)4,
            index => Kernels.SharedAddressKernel(index, pointers.View, v2));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0; i < 3; i++)
            Console.WriteLine(string.Join(" ", result[(4 * i)..(4 * i + 4)]));
    }
}
