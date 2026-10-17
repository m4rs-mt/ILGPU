// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: type tests with patterns in generic code, which box the value.
// Classify<T> is launched through a generic wrapper with float and with int.
// Expected output: 1 1 2 1 | 2 2 4.5 1.5

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Classify<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> result)
        where T : unmanaged, IEquatable<T>
    {
        var value = data[index];
        float scaled;
        if (value is float f)
            scaled = f * 2.0f;
        else if (value is int i)
            scaled = i + 0.5f;
        else
            scaled = -1.0f;

        // Lane 0 reports the result of the type tests, the others the value
        result[index] = index == 0
            ? (default(T) is float ? 1.0f : 2.0f)
            : index == 1
                ? (value.Equals(default) ? 1.0f : 2.0f)
                : scaled;
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

        Run(stream, new float[] { 0.0f, 0.0f, 1.0f, 0.5f });
        Run(stream, new int[] { 7, 3, 4, 1 });
    }

    static void Run<T>(AcceleratorStream stream, T[] values)
        where T : unmanaged, IEquatable<T>
    {
        using var data = stream.Allocate1D(values);
        using var result = stream.Allocate1D<float>(values.Length);
        var dataView = data.View;
        var resultView = result.View;
        stream.Launch(
            (Index1D)values.Length,
            i => Kernels.Classify(i, dataView, resultView));
        stream.Synchronize();
        foreach (var value in result.GetAsArray1D())
            Console.WriteLine(value.ToString(CultureInfo.InvariantCulture));
    }
}
