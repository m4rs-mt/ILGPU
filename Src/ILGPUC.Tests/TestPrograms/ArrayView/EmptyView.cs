// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: EmptyView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: EmptyView
// Kernels read the members of constant views without elements: the empty view
// and a default view. Every result buffer is filled with -1 beforehand.
// Expected output:
//   0 0 0 0                EmptyIntLengthKernel: Empty.IntLength of 4 threads
//   0 0 0 0 0 0 0 (x2)     EmptyMembersKernel: IntLength, Length,
//                          LengthInBytes, Extent.X, IsValid,
//                          SubView(0, 0).Length, Cast<long>().Length
//   0 0 0 0 0 0 0 (x2)     DefaultMembersKernel: the same of a default view

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void EmptyIntLengthKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        result[index] = ArrayView<int>.Empty.IntLength;
    }

    public static void EmptyMembersKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        var view = ArrayView<int>.Empty;
        result[index * 7 + 0] = view.IntLength;
        result[index * 7 + 1] = view.Length;
        result[index * 7 + 2] = view.LengthInBytes;
        result[index * 7 + 3] = view.Extent.X;
        result[index * 7 + 4] = view.IsValid ? 1 : 0;
        result[index * 7 + 5] = view.SubView(0, 0).Length;
        result[index * 7 + 6] = view.Cast<long>().Length;
    }

    public static void DefaultMembersKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> result)
    {
        ArrayView<int> view = default;
        result[index * 7 + 0] = view.IntLength;
        result[index * 7 + 1] = view.Length;
        result[index * 7 + 2] = view.LengthInBytes;
        result[index * 7 + 3] = view.Extent.X;
        result[index * 7 + 4] = view.IsValid ? 1 : 0;
        result[index * 7 + 5] = view.SubView(0, 0).Length;
        result[index * 7 + 6] = view.Cast<long>().Length;
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

        using var lengths = stream.Allocate1D(new int[] { -1, -1, -1, -1 });
        stream.Launch(
            (Index1D)4,
            index => Kernels.EmptyIntLengthKernel(index, lengths.View));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", lengths.GetAsArray1D()));

        var initial = new long[14];
        Array.Fill(initial, -1L);

        using var members = stream.Allocate1D(initial);
        stream.Launch(
            (Index1D)2,
            index => Kernels.EmptyMembersKernel(index, members.View));
        stream.Synchronize();
        Print(members.GetAsArray1D());

        using var defaults = stream.Allocate1D(initial);
        stream.Launch(
            (Index1D)2,
            index => Kernels.DefaultMembersKernel(index, defaults.View));
        stream.Synchronize();
        Print(defaults.GetAsArray1D());
    }

    static void Print(long[] values)
    {
        for (int i = 0; i < values.Length; i += 7)
            Console.WriteLine(string.Join(" ", values[i..(i + 7)]));
    }
}
