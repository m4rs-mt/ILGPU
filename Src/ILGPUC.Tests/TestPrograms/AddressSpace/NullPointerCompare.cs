// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullPointerCompare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NullPointerCompare
// Pointers into local and global memory compared with null, which C# emits as a
// compare with the integer zero. Thread t:
// - LocalAddressNullKernel: &value is not null and a pointer local that holds
//   null is null, writes t + 7;
// - NullPointerPhiKernel: the pointer is &value for even and null for odd
//   threads, compared with null in a helper and in the kernel, writes
//   101 * (t + 7) or -101;
// - NullPointerPhisKernel: a uniform and a per-thread pointer start as null and
//   take &data[0] and &value, writes 2 * 10 + t;
// - SharedNullPointerKernel: the pointer is the address of the thread's
//   shared-memory element for even and null for odd threads, writes t + 1 or
//   -1.
// Expected output: 7 8 9 10, 707 -101 909 -101, 20 21 22 23, 1 -1 3 -1

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static unsafe int ValueOrMinusOne(int* p) => p == null ? -1 : *p + 7;

    public static unsafe void LocalAddressNullKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X;
        int* p = &value;
        int* none = null;
        data[index] = (p == null ? -1 : *p + 7) + (none == null ? 0 : 100);
    }

    public static unsafe void NullPointerPhiKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X;
        int* p = null;
        if ((index.X & 1) == 0)
            p = &value;
        data[index] = ValueOrMinusOne(p) * 100 + (p == null ? -1 : *p + 7);
    }

    public static unsafe void NullPointerPhisKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X;
        fixed (int* first = &data[0])
        {
            int* p = null;
            int* q = null;
            if (data.IntLength > 2)
            {
                p = first;
                q = &value;
            }
            data[index] = (p == null ? -1 : 2) * 10 + (q == null ? -1 : *q);
        }
    }

    public static unsafe void SharedNullPointerKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var shared = Group.GetSharedMemory<int>(32);
        int g = Group.Index;
        shared[g] = index.X + 1;
        Group.Barrier();
        fixed (int* p = &shared[g])
        {
            int* q = index.X % 2 == 0 ? p : null;
            data[index] = q == null ? -1 : *q;
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

        using var address = stream.Allocate1D<int>(4);
        using var phi = stream.Allocate1D<int>(4);
        using var phis = stream.Allocate1D<int>(4);
        using var shared = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.LocalAddressNullKernel(index, address.View));
        stream.Launch((Index1D)4, index => Kernels.NullPointerPhiKernel(index, phi.View));
        stream.Launch((Index1D)4, index => Kernels.NullPointerPhisKernel(index, phis.View));
        stream.Launch((Index1D)4, index => Kernels.SharedNullPointerKernel(index, shared.View));
        stream.Synchronize();

        foreach (var buffer in new[] { address, phi, phis, shared })
        {
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
