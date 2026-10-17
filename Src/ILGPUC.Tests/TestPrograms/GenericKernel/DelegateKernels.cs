// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: generic methods launch a generic kernel taking a Func with a lambda
// literal, once plain and once capturing a value of the method's type parameter. The
// launches are lifted as inline kernels of generic code and compiled once per
// instantiation of the launching method.
// Expected output: 2 4 6 | 1 3 5 | 11 12 13 | 10.5 11.5 12.5

using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Map<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        Func<T, T> op)
        where T : unmanaged =>
        data[index] = op(data[index]);
}

static class Program
{
    static T[] Twice<T>(AcceleratorStream stream, T[] values)
        where T : unmanaged, INumber<T>
    {
        using var buffer = stream.Allocate1D(values);
        var view = buffer.View;
        stream.Launch((Index1D)values.Length, i => Kernels.Map(i, view, x => x + x));
        stream.Synchronize();
        return buffer.GetAsArray1D();
    }

    static T[] AddOffset<T>(AcceleratorStream stream, T[] values, T offset)
        where T : unmanaged, INumber<T>
    {
        using var buffer = stream.Allocate1D(values);
        var view = buffer.View;
        stream.Launch(
            (Index1D)values.Length,
            i => Kernels.Map(i, view, x => x + offset));
        stream.Synchronize();
        return buffer.GetAsArray1D();
    }

    static void Print<T>(T[] values) =>
        Console.WriteLine(string.Join(
            " ",
            values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))));

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        Print(Twice(stream, new[] { 1, 2, 3 }));
        Print(Twice(stream, new[] { 0.5f, 1.5f, 2.5f }));
        Print(AddOffset(stream, new[] { 1, 2, 3 }, 10));
        Print(AddOffset(stream, new[] { 0.5f, 1.5f, 2.5f }, 10f));
    }
}
