// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NativeIntegerUnary.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NativeIntegerUnary
// Kernel negates and inverts a native integer made from each value x, writes
// through the address of its element aligned with the mask ~(nint)3, which is the
// same address, and writes 1000 to field B through the address of field A moved
// by 4 bytes: -100 * x + (-x - 1) + 1000.
// Expected output: -11 -1021 -2031 -3041

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

struct Pair
{
    public int A;
    public int B;
}

static class Kernels
{
    public static unsafe void NativeIntegerUnaryKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Pair pair;
        pair.A = 0;
        pair.B = 0;
        nint n = data[index];
        nint negated = -n;
        nint inverted = ~n;
        int* element = (int*)Unsafe.AsPointer(ref data[index]);
        int* aligned = (int*)((nint)element & ~(nint)3);
        nint address = (nint)(&pair.A);
        address += 4;
        *(int*)address = 1000;
        *aligned = (int)(negated * 100 + inverted) + pair.B;
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

        using var buffer = stream.Allocate1D<int>(4);
        buffer.View.CopyFromCPU(stream, new[] { 10, 20, 30, 40 });
        stream.Launch((Index1D)4, index => Kernels.NativeIntegerUnaryKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
