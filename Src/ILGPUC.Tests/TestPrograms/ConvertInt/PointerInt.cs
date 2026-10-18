// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerInt.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PointerInt
// Kernel converts pointers to and from integers: (long)&x, (nint)&x, (int*)address,
// (int*)native, an address stored to memory and converted back to a pointer, and
// the difference of two pointers.
// Expected output per lane: 1, x, x + 1, x + 2 and 4 for x = 10 * index + 3.

using System;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    public static void PointerIntKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        int i = index;
        int x = 10 * i + 3;
        long address = (long)&x;
        nint native = (nint)(&x);
        int* fromLong = (int*)address;
        int* fromNative = (int*)native;
        int o = i * 5;
        output[o] = address != 0 && address == native ? 1 : 0;
        output[o + 1] = *fromLong;
        output[o + 2] = *fromNative + 1;
        fixed (long* first = &output[o])
        fixed (long* last = &output[o + 4])
        {
            output[o + 3] = (long)(first + 2);
            long* stored = (long*)output[o + 3];
            output[o + 3] = *stored + 1;
            *last = last - first;
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

        using var output = stream.Allocate1D<long>(20);
        stream.Launch(
            (Index1D)4,
            index => Kernels.PointerIntKernel(index, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        for (int i = 0; i < 4; ++i)
            Console.WriteLine(string.Join(" ", data[(i * 5)..(i * 5 + 5)]));
    }
}
