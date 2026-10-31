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
// Kernels select structures holding pointers into different address spaces,
// store pointers to locals in a local array of structures and give a structure
// local holding pointers to locals a pointer to a view element after a loop.
// Expected output: 110 220 310 420, 115023 125033 135043 145053, 322 342 362 382

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
/// A pointer incremented by an instance method.
/// </summary>
public unsafe struct Counter
{
    public int* P;

    public void Increment() => *P += 1;
}

/// <summary>
/// Three adjacent integers.
/// </summary>
public struct Triple
{
    public int A;
    public int B;
    public int C;
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

    /// <summary>
    /// A local array of holders of pointers to the fields of a local structure
    /// (CPU, CUDA and ROCm only: Metal and OpenCL reject pointers to local
    /// memory stored in memory that holds generic pointers).
    /// </summary>
    public static void HolderArrayKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2;
        t.C = 3;
        var holders = new Holder[3];
        holders[0].P = &t.A;
        holders[1].P = &t.B;
        holders[2].P = &t.C;
        int sum = 0;
        for (int k = 0; k < holders.Length; ++k)
        {
            holders[k].N = k + 1;
            sum += *holders[k].P * holders[k].N;
        }
        *holders[index.X % 3].P += 100;
        data[index] = sum + 1000 * (t.A + t.B + t.C);
    }

    /// <summary>
    /// Structures holding a single pointer to a local, one of which is selected
    /// in a loop, incremented through an instance method and assigned to the
    /// first, which is given a pointer to a view element after the loop (CPU,
    /// CUDA and ROCm only: at O0, Metal and OpenCL reject pointers to local
    /// memory stored in memory that holds generic pointers).
    /// </summary>
    public static void LaterGlobalCounterKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 0;
        Counter a;
        a.P = &x;
        Counter b;
        b.P = &y;
        for (int k = 0; k < 4; ++k)
        {
            Counter c = (k & 1) == 0 ? a : b;
            c.Increment();
            a = c;
        }
        int sum = x + 100 * y;
        a.P = (int*)Unsafe.AsPointer(ref data[index]);
        a.Increment();
        data[index] += sum;
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
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.HolderArrayKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.LaterGlobalCounterKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
