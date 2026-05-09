// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericClosure.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: GenericKernel / GenericClosure
// Generic kernel parameterised over a struct closure with a captured offset.
// 4 threads write `Offset + value * index` to data; expected output: 20 21 22 23

using System;
using ILGPU;
using ILGPU.Runtime;

interface IClosure<T> where T : struct
{
    T Compute(Index1D index, int value);
}

readonly struct AddOffsetClosure : IClosure<long>
{
    public AddOffsetClosure(long offset) { Offset = offset; }
    public long Offset { get; }
    public long Compute(Index1D index, int value) => Offset + value * index;
}

static class Kernels
{
    public static void LaunchClosureKernel<TClosure, T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> data,
        int value,
        TClosure closure)
        where TClosure : struct, IClosure<T>
        where T : unmanaged
    {
        data[index] = closure.Compute(index, value);
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

        const int Length = 4;
        using var buffer = stream.Allocate1D<long>(Length);
        var closure = new AddOffsetClosure(20);

        stream.Launch(
            (Index1D)Length,
            index => Kernels.LaunchClosureKernel(index, buffer.View, 1, closure));
        stream.Synchronize();

        foreach (var v in buffer.GetAsArray1D())
            Console.WriteLine(v);
    }
}
