// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnsignedElementOffset.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: UnsignedElementOffset
// Kernel offsets a reference 2^31 elements before the buffer by unsigned 32-bit
// offsets from 2^31 on, widened to long, which zero-extends them and lands in
// the buffer.
// Expected output: 10 11 12 13

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void UnsignedElementOffsetKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<uint, Stride1D.Dense> offsets,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        ref int origin = ref Unsafe.Add(ref data[0], (nint)int.MinValue);
        output[index] = Unsafe.Add(ref origin, (nint)(long)offsets[index]);
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

        using var data = stream.Allocate1D(new int[] { 10, 11, 12, 13 });
        using var offsets = stream.Allocate1D(
            new uint[] { 0x80000000u, 0x80000001u, 0x80000002u, 0x80000003u });
        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.UnsignedElementOffsetKernel(
                index,
                data.View,
                offsets.View,
                output.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
    }
}
