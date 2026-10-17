// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: OverloadedKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: overloaded kernels in a generic class, launched from a method of
// that class for two type arguments.
// Expected output: 2 4 | 3 6 | 10 20

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Ops<T> where T : unmanaged, INumber<T>
{
    public static void Scale(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        T factor)
    {
        data[index] *= factor;
    }

    public static void Scale(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int factor)
    {
        data[index] *= factor * 10;
    }

    public static void Run(AcceleratorStream stream, T[] values, T factor)
    {
        using var buffer = stream.Allocate1D(values);
        var view = buffer.View;
        stream.Launch((Index1D)values.Length, i => Scale(i, view, factor));
        stream.Synchronize();
        foreach (var value in buffer.GetAsArray1D())
            Console.WriteLine(value);
    }

    public static void RunInt(AcceleratorStream stream)
    {
        using var buffer = stream.Allocate1D(new int[] { 1, 2 });
        var view = buffer.View;
        stream.Launch((Index1D)2, i => Scale(i, view, 1));
        stream.Synchronize();
        foreach (var value in buffer.GetAsArray1D())
            Console.WriteLine(value);
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

        Ops<int>.Run(stream, new int[] { 1, 2 }, 2);
        Ops<long>.Run(stream, new long[] { 1, 2 }, 3L);
        Ops<short>.RunInt(stream);
    }
}
