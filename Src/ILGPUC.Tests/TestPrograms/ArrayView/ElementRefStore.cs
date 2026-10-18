// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ElementRefStore.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: ElementRefStore
// Kernel takes a reference to a view element, stores other elements, and then
// stores through the reference and passes it to a helper that increments it.
// With 4 threads t: data[3t] = 10 or 20 (even or odd t), data[3t + 1] = 30 if t
// is a multiple of 3 and 40 otherwise, data[3t + 2] = 7 + 1.
// Expected output: one line per thread.

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Increment(ref long target) => target += 1;

    public static void ElementRefStoreKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        int i = index;
        int o = i * 3;
        ref long element = ref data[o + 2];
        data[o] = i % 2 == 0 ? 10 : 20;
        data[o + 1] = i % 3 == 0 ? 30 : 40;
        element = 7;
        Increment(ref element);
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

        using var buffer = stream.Allocate1D<long>(12);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)4,
            index => Kernels.ElementRefStoreKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0; i < 4; ++i)
            Console.WriteLine(string.Join(" ", data[(i * 3)..(i * 3 + 3)]));
    }
}
