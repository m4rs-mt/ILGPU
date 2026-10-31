// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalAddressRoundTrip.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: LocalAddressRoundTrip
// The first kernel writes each value x through the address of a local variable
// converted to a long and back to a pointer. The second kernel passes that
// address as a native integer in a field of a structure to a helper, which
// writes x + x through it. Both addresses keep pointing to local memory, also
// on backends without generic pointers.
// Expected output: 0 0, 1 2, 2 4, 3 6

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

struct NativeValue
{
    public nint Value;
    public int Count;
}

static unsafe class Kernels
{
    public static void RoundTripKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 0;
        long address = (long)&value;
        *(int*)address = data[index];
        data[index] = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void WriteTo(NativeValue target, int value) =>
        *(int*)target.Value = value + target.Count;

    public static void FieldKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 0;
        NativeValue target = default;
        target.Value = (nint)(long)&value;
        target.Count = data[index];
        WriteTo(target, data[index]);
        data[index] = value;
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

        using var roundTrip = stream.Allocate1D<int>(4);
        roundTrip.View.CopyFromCPU(stream, new[] { 0, 1, 2, 3 });
        stream.Launch((Index1D)4, index => Kernels.RoundTripKernel(index, roundTrip.View));

        using var field = stream.Allocate1D<int>(4);
        field.View.CopyFromCPU(stream, new[] { 0, 1, 2, 3 });
        stream.Launch((Index1D)4, index => Kernels.FieldKernel(index, field.View));
        stream.Synchronize();

        var first = roundTrip.GetAsArray1D();
        var second = field.GetAsArray1D();
        for (int i = 0; i < 4; ++i)
            Console.WriteLine($"{first[i]} {second[i]}");
    }
}
