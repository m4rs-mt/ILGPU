// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConstantNativeWidening.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: ConstantNativeWidening
// A recursive helper called with constant arguments is evaluated at compile time
// and converts unsigned 32-bit values from 2^31 on to nuint, which zero-extends
// them.
// Expected output: 2147483648 4294967295 2147483648 4294967295

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static ulong Widen(uint value, int depth) =>
        depth == 0 ? (ulong)(nuint)value : Widen(value, depth - 1);

    public static void ConstantNativeWideningKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> data)
    {
        data[index] = index % 2 == 0
            ? (long)Widen(0x80000000u, 2)
            : (long)Widen(uint.MaxValue, 1);
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

        using var buffer = stream.Allocate1D<long>(4);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)4,
            index => Kernels.ConstantNativeWideningKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
