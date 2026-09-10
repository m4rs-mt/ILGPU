// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineTypeTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: an inline lambda kernel in a generic method selects its arithmetic
// with typeof(T) tests. The kernel is compiled once per instantiation of the method,
// and the tests and the throwing fallback are removed at compile time.
// Expected output: 2 4 6 | 1 3 5

using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Program
{
    static T[] Twice<T>(AcceleratorStream stream, T[] values) where T : unmanaged
    {
        using var buffer = stream.Allocate1D(values);
        var view = buffer.View;
        stream.Launch((Index1D)values.Length, i =>
        {
            if (typeof(T) == typeof(int))
                view[i] = Unsafe.BitCast<int, T>(Unsafe.BitCast<T, int>(view[i]) * 2);
            else if (typeof(T) == typeof(float))
                view[i] = Unsafe.BitCast<float, T>(Unsafe.BitCast<T, float>(view[i]) * 2);
            else
                throw new NotSupportedException();
        });
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
    }
}
