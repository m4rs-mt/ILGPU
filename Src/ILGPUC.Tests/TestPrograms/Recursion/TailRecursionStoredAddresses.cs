// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TailRecursionStoredAddresses.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: TailRecursionStoredAddresses
// Helpers store the address of a local variable to memory, directly or through a
// method they pass the variable to, before they call themselves in tail position,
// and every call reads the variable of its caller through that memory. They stay
// recursive, as a loop would use one variable for all calls.
// Expected output: 0 0 2 2 5 5 9 9 14 14 20 20 27 27 35 35

using System;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    static int SumCallers(
        int n, ArrayView1D<nint, Stride1D.Dense> slots, int slot, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        int caller = *(int*)slots[slot];
        slots[slot] = (nint)(&x);
        return SumCallers(n - 1, slots, slot, sum + caller);
    }

    static void Remember(ref int value, ArrayView1D<nint, Stride1D.Dense> slots, int slot)
    {
        fixed (int* address = &value)
            slots[slot] = (nint)address;
    }

    static int SumRemembered(
        int n, ArrayView1D<nint, Stride1D.Dense> slots, int slot, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        int caller = *(int*)slots[slot];
        Remember(ref x, slots, slot);
        return SumRemembered(n - 1, slots, slot, sum + caller);
    }

    public static void TailRecursionStoredAddressesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<nint, Stride1D.Dense> slots)
    {
        int start = 0;
        slots[index * 2] = (nint)(&start);
        slots[index * 2 + 1] = (nint)(&start);
        data[index * 2] = SumCallers(index + 1, slots, index * 2, 0);
        data[index * 2 + 1] = SumRemembered(index + 1, slots, index * 2 + 1, 0);
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

        using var buffer = stream.Allocate1D<int>(16);
        using var slots = stream.Allocate1D<nint>(16);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)8,
            index => Kernels.TailRecursionStoredAddressesKernel(
                index, buffer.View, slots.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
