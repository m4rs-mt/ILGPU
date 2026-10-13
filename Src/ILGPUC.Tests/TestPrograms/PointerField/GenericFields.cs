// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericFields.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerField GenericFields
// Kernel selects structures holding pointers into different address spaces.
// Expected output: 110 220 310 420

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

/// <summary>
/// A pointer and a count.
/// </summary>
public unsafe struct Holder
{
    public int* P;
    public int N;
}

static unsafe class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ReadHolder(Holder h) => *h.P + h.N;

    /// <summary>
    /// A holder of a pointer to a local or of a pointer to a view element,
    /// selected by a condition and passed to a helper that is not inlined (CPU,
    /// CUDA and ROCm only: Metal and OpenCL reject pointers that can point to
    /// global and local memory).
    /// </summary>
    public static void SelectHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder a;
        a.P = &x;
        a.N = 1;
        Holder b;
        b.P = (int*)Unsafe.AsPointer(ref data[index]);
        b.N = 2;
        data[index] = 10 * ReadHolder((index.X & 1) == 0 ? a : b);
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
            stream.Launch((Index1D)4, index => Kernels.SelectHolderKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
