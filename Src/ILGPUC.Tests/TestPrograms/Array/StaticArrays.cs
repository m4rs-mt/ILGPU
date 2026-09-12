// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StaticArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: StaticArrays
// Kernels read lookup tables from static fields: an ImmutableArray, a 1D array of
// structs and a 2D array. The arrays are inlined with their contents at compile
// time (the plain arrays need ArrayMode.InlineMutableStaticArrays).
// Expected output: one line per lane and kernel.

using System;
using System.Collections.Immutable;
using ILGPU;
using ILGPU.Runtime;

struct Entry
{
    public int Scale;
    public int Offset;
}

static class Kernels
{
    static readonly ImmutableArray<int> Primes = [2, 3, 5, 7];

    static readonly Entry[] Entries =
    [
        new() { Scale = 1, Offset = 10 },
        new() { Scale = 2, Offset = 20 },
        new() { Scale = 3, Offset = 30 },
        new() { Scale = 4, Offset = 40 },
    ];

    static readonly int[,] Grid = { { 1, 2 }, { 3, 4 } };

    static int Transform(int index)
    {
        var entry = Entries[index];
        return entry.Scale * index + entry.Offset;
    }

    public static void PrimesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = Primes[index] * Primes.Length;
    }

    public static void EntriesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = Transform(index);
    }

    public static void GridKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = Grid[index / 2, index % 2] * 100 + index;
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

        using var output = stream.Allocate1D<int>(4);
        var view = output.View;

        stream.Launch((Index1D)4, i => Kernels.PrimesKernel(i, view));
        stream.Synchronize();
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);

        stream.Launch((Index1D)4, i => Kernels.EntriesKernel(i, view));
        stream.Synchronize();
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);

        stream.Launch((Index1D)4, i => Kernels.GridKernel(i, view));
        stream.Synchronize();
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
