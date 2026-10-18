// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AsPointer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: AsPointer
// Kernel takes the addresses of a view element and of a local with
// Unsafe.AsPointer, increments the local by 100 through its pointer and stores
// 10 * element + local through the pointer to the element.
// With 4 threads: data = [1, 2, 3, 4]
// Expected output: 110 121 132 143

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    public static void AsPointerKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int* element = (int*)Unsafe.AsPointer(ref data[index]);
        int local = index;
        int* pointer = (int*)Unsafe.AsPointer(ref local);
        *pointer += 100;
        *element = *element * 10 + local;
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

        using var buffer = stream.Allocate1D(new int[] { 1, 2, 3, 4 });
        stream.Launch((Index1D)4, index => Kernels.AsPointerKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
