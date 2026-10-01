// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypedPointers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerArithmetic TypedPointers
// A kernel reads bytes of an integer and of a structure through byte pointers.
// Expected output: 40240 33250 32260 31270

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

struct Triple
{
    public int A;
    public int B;
    public int C;
}

static unsafe class Kernels
{
    /// <summary>
    /// Bytes of an integer and of a structure read through byte pointers,
    /// including byte offsets of an int pointer.
    /// </summary>
    public static void BytesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index] + 0x01020300;
        byte* b = (byte*)&x;
        Triple t;
        t.A = 0x0100;
        t.B = 2;
        t.C = 3;
        int* p = &t.A;
        data[index] = b[0] + 10 * b[1] + 100 * *(b + 2) + 1000 * b[index.X & 3] +
            10000 * (*((byte*)p + 1) + *(int*)((byte*)p + 4));
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

        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.BytesKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
