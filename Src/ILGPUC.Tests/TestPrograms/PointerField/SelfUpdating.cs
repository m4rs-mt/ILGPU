// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SelfUpdating.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerField SelfUpdating
// Kernels assign holders of pointers to locals values derived from their own
// values in loops, also through an instance method.
// Expected output: 30 30 50 30, 30 30 50 30, 311 321 331 341

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
/// A count and a pointer that is not the first field.
/// </summary>
public unsafe struct CountedHolder
{
    public int N;
    public int* P;
}

/// <summary>
/// A pointer incremented by an instance method.
/// </summary>
public unsafe struct Counter
{
    public int* P;

    public void Increment() => *P += 1;
}

static unsafe class Kernels
{
    /// <summary>
    /// Holders of pointers to two locals, one of which is selected in a loop
    /// and assigned to the first: the first holder is updated with values
    /// derived from its own value.
    /// </summary>
    public static void SelfUpdatingHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 5;
        Holder a;
        a.P = &x;
        a.N = 1;
        Holder b;
        b.P = &y;
        b.N = 2;
        int sum = 0;
        for (int k = 0; k < 3; ++k)
        {
            Holder c = ((k + index.X) & 1) == 0 ? a : b;
            sum += *c.P * c.N;
            a = c;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Holders with a pointer to a local in their second field, one of which
    /// is selected in a loop and assigned to the first.
    /// </summary>
    public static void SelfUpdatingCountedKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index];
        int y = 5;
        CountedHolder a;
        a.N = 1;
        a.P = &x;
        CountedHolder b;
        b.N = 2;
        b.P = &y;
        int sum = 0;
        for (int k = 0; k < 3; ++k)
        {
            CountedHolder c = ((k + index.X) & 1) == 0 ? a : b;
            sum += *c.P * c.N;
            a = c;
        }
        data[index] = sum;
    }

    /// <summary>
    /// Structures holding a single pointer to a local, one of which is selected
    /// in a loop, incremented through an instance method and assigned to the
    /// first.
    /// </summary>
    public static void SelfUpdatingCounterKernel(
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
        data[index] = x + 100 * y;
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
            stream.Launch((Index1D)4, index => Kernels.SelfUpdatingHolderKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.SelfUpdatingCountedKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.SelfUpdatingCounterKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
