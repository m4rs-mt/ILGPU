// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NarrowUnsignedWidening.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: NarrowUnsignedWidening
// Kernel converts sbyte, byte, short and ushort values to uint and widens them to
// ulong and nuint, which extends 8 and 16-bit values with their own signs to 32
// bits and zero-extends those: values loaded from buffers, values converted from
// int, a constant passed to a helper and nuint offsets of a reference 2^32 - 1
// elements before the last element of the int buffer.
// Expected output per thread: the host results of the same conversions.

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static ulong WidenShort(short value) => (ulong)(uint)value;

    public static void NarrowUnsignedWideningKernel(
        Index1D index,
        ArrayView1D<sbyte, Stride1D.Dense> sbytes,
        ArrayView1D<byte, Stride1D.Dense> bytes,
        ArrayView1D<short, Stride1D.Dense> shorts,
        ArrayView1D<ushort, Stride1D.Dense> ushorts,
        ArrayView1D<int, Stride1D.Dense> ints,
        ArrayView1D<short, Stride1D.Dense> negatives,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        int x = ints[index];
        int o = index * 12;
        output[o] = (long)(ulong)(uint)sbytes[index];
        output[o + 1] = (long)(ulong)(uint)shorts[index];
        output[o + 2] = (long)(ulong)(uint)bytes[index];
        output[o + 3] = (long)(ulong)(uint)ushorts[index];
        output[o + 4] = (long)(ulong)(uint)(sbyte)x;
        output[o + 5] = (long)(ulong)(uint)(short)x;
        output[o + 6] = (long)(ulong)(uint)(byte)x;
        output[o + 7] = (long)(ulong)(uint)(ushort)x;
        output[o + 8] = (long)(ulong)sbytes[index];
        output[o + 9] = (long)WidenShort(-1);

        ref int origin = ref Unsafe.Add(ref ints[7], (nint)(-0xFFFFFFFFL));
        output[o + 10] = Unsafe.Add(ref origin, (nuint)(uint)negatives[index]);
        output[o + 11] = Unsafe.Add(ref origin, (nuint)(uint)(short)(-1 - index.X));
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

        using var sbytes = stream.Allocate1D(new sbyte[] { -1, -128, 127, 5 });
        using var bytes = stream.Allocate1D(new byte[] { 255, 128, 127, 5 });
        using var shorts = stream.Allocate1D(new short[] { -1, -32768, 32767, 5 });
        using var ushorts = stream.Allocate1D(new ushort[] { 65535, 32768, 32767, 5 });
        using var ints = stream.Allocate1D(
            new int[] { -1, 0x18000, 0x1FF80, 5, 10, 11, 12, 13 });
        using var negatives = stream.Allocate1D(new short[] { -1, -2, -3, -4 });
        using var output = stream.Allocate1D<long>(48);
        stream.Launch(
            (Index1D)4,
            index => Kernels.NarrowUnsignedWideningKernel(
                index,
                sbytes.View,
                bytes.View,
                shorts.View,
                ushorts.View,
                ints.View,
                negatives.View,
                output.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        for (int i = 0; i < 4; ++i)
            Console.WriteLine(string.Join(" ", values[(i * 12)..(i * 12 + 12)]));
    }
}
