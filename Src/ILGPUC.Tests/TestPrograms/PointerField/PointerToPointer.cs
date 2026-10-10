// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerToPointer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerField PointerToPointer
// Kernel writes and reads a pointer local holding the address of a view element
// through a pointer to it.
// Expected output: 121 231 341 451

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    /// <summary>
    /// An address-taken pointer local holding the address of a view element.
    /// </summary>
    public static void ViewPointerToPointerKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int* q = (int*)Unsafe.AsPointer(ref data[index]);
        int** pp = &q;
        **pp += 1;
        data[index] += 10 * **pp;
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
            stream.Launch((Index1D)4, index => Kernels.ViewPointerToPointerKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
