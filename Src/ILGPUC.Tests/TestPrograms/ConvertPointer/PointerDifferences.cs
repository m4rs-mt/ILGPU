// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerDifferences.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerDifferences
// Kernel subtracts the addresses of the first and the last element from the
// address of each element as int pointers, in elements (index and index - 3), and
// as byte pointers (4 * index): x + 1000 * index + 100 * (index - 3) + 4 * index.
// Expected output: -290 824 1938 3052

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static unsafe void PointerDifferencesKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int* first = (int*)Unsafe.AsPointer(ref data[0]);
        int* last = (int*)Unsafe.AsPointer(ref data[3]);
        int* element = (int*)Unsafe.AsPointer(ref data[index]);
        long forward = element - first;
        long backward = element - last;
        long bytes = (byte*)element - (byte*)first;
        data[index] += (int)(forward * 1000 + backward * 100 + bytes);
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
        stream.Launch((Index1D)4, index => Kernels.PointerDifferencesKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
