// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Escapes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerField Escapes
// Kernels pass the addresses of a holder and of a pointer local to helpers that
// are not inlined and replace a pointer field through the address of another.
// Expected output: 13 23 33 43, 11 21 31 41, 33 53 73 93

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

/// <summary>
/// Two pointers.
/// </summary>
public unsafe struct PointerPair
{
    public int* A;
    public int* B;
}

static unsafe class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddByRef(ref Holder h) => *h.P += h.N;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Increment(int** pp) => **pp += 1;

    /// <summary>
    /// A holder of a pointer to a local passed by reference to a helper that is
    /// not inlined, whose pointer is compared with the address of the local
    /// afterwards (CPU, CUDA and ROCm only: Metal and OpenCL reject pointers to
    /// local memory stored in memory whose address escapes to a helper).
    /// </summary>
    public static void HolderByRefKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        Holder h;
        h.P = &x;
        h.N = 3;
        AddByRef(ref h);
        data[index] = h.P == &x ? x : -1;
    }

    /// <summary>
    /// The address of a pointer local holding the address of a local passed to
    /// a helper that is not inlined, compared with the address of the local
    /// afterwards (CPU, CUDA and ROCm only: Metal and OpenCL reject pointers to
    /// local memory stored in memory whose address escapes to a helper).
    /// </summary>
    public static void PointerToPointerHelperKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int* q = &x;
        Increment(&q);
        data[index] = q == &x ? x : -1;
    }

    /// <summary>
    /// Pointers to a view element and to a local in a local structure, where the
    /// second is replaced by a pointer to the view element through an element
    /// address relative to the first field (CPU, CUDA and ROCm only: Metal and
    /// OpenCL reject pointers to local memory stored in memory that holds
    /// generic pointers).
    /// </summary>
    public static void FieldOffsetKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int y = 3;
        PointerPair pair;
        pair.A = (int*)Unsafe.AsPointer(ref data[index]);
        pair.B = &y;
        int** fields = &pair.A;
        fields[1] = (int*)Unsafe.AsPointer(ref data[index]);
        *pair.B += 5;
        data[index] = *pair.A * 2 + y;
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
            stream.Launch((Index1D)4, index => Kernels.HolderByRefKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.PointerToPointerHelperKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.FieldOffsetKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
