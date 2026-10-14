// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MixedStoredLoops.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerLoop MixedStoredLoops
// Kernel keeps pointers to a local and to a view element in a structure field
// changed in a loop.
// Expected output: 33 63 93 123

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

/// <summary>
/// A pointer and a count.
/// </summary>
public unsafe struct Cursor
{
    public int* P;
    public int N;
}

static unsafe class Kernels
{
    /// <summary>
    /// A pointer field of a local structure that points to a local on the
    /// first iteration and to a view element or the local on the next ones
    /// (CPU, CUDA and ROCm only: Metal and OpenCL reject pointers that can
    /// point to global and local memory).
    /// </summary>
    public static void MixedFieldLoopKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Cursor c;
        c.P = &x;
        c.N = 0;
        int sum = 0;
        for (int k = 0; k < 3; ++k)
        {
            sum += *c.P;
            c.P = (k & 1) == 0 ? (int*)Unsafe.AsPointer(ref data[index]) : &x;
            c.N++;
        }
        data[index] = sum + c.N;
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
            stream.Launch((Index1D)4, index => Kernels.MixedFieldLoopKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
