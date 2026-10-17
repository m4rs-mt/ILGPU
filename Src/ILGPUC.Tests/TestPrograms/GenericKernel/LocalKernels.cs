// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: inline lambda kernels and local function kernels in generic code,
// compiled once per instantiation of the code that contains them. An inline kernel in
// a generic method captures a local of a type parameter, an inline kernel in a method
// of a generic class captures parameters of the class's type parameter, and a local
// function kernel of a generic method takes values of the method's type parameter.
// Expected output: 1 1 1 1 | 2.5 2.5 2.5 2.5 | 3 4 3 4 | 0.5 1.5 0.5 1.5 |
//                  7 7 8 8 | 5 5 6 6

using System;
using System.Globalization;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

sealed class Runner<T> where T : unmanaged
{
    public T[] Alternate(AcceleratorStream stream, T even, T odd)
    {
        using var buffer = stream.Allocate1D<T>(4);
        var view = buffer.View;
        stream.Launch((Index1D)4, i => view[i] = i.X % 2 == 0 ? even : odd);
        stream.Synchronize();
        return buffer.GetAsArray1D();
    }
}

static class Program
{
    static T[] Fill<T>(AcceleratorStream stream, T value) where T : unmanaged
    {
        using var buffer = stream.Allocate1D<T>(4);
        var view = buffer.View;
        T fill = value;
        stream.Launch((Index1D)4, i => view[i] = fill);
        stream.Synchronize();
        return buffer.GetAsArray1D();
    }

    static T[] Halves<T>(AcceleratorStream stream, T first, T second)
        where T : unmanaged
    {
        static void Kernel(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> data,
            T low,
            T high) =>
            data[index] = index.X < 2 ? low : high;

        using var buffer = stream.Allocate1D<T>(4);
        var view = buffer.View;
        stream.Launch((Index1D)4, i => Kernel(i, view, first, second));
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

        Print(Fill(stream, 1));
        Print(Fill(stream, 2.5f));
        Print(new Runner<int>().Alternate(stream, 3, 4));
        Print(new Runner<float>().Alternate(stream, 0.5f, 1.5f));
        Print(Halves(stream, 7u, 8u));
        Print(Halves<short>(stream, 5, 6));
    }
}
