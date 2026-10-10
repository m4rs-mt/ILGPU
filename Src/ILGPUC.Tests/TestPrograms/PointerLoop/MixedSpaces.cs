// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MixedSpaces.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerLoop MixedSpaces
// Kernel compares the address of a local with the address of a view element.
// Expected output: 12 22 32 42

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    /// <summary>
    /// The address of a local compared with the address of a view element
    /// (CPU, CUDA and ROCm only: Metal and OpenCL reject comparisons of
    /// pointers into different address spaces).
    /// </summary>
    public static void MixedSpaceKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int* e = (int*)Unsafe.AsPointer(ref data[index]);
        // &x < e && e < &x is always false: it only compares the pointers
        data[index] = &x == e || &x < e && e < &x ? 1 : 2 + x;
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
            stream.Launch((Index1D)4, index => Kernels.MixedSpaceKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
