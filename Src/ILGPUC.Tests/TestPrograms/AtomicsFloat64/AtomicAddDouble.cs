// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicAddDouble.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: AtomicsFloat64 / AtomicAddDouble
// 4 threads atomically add 2.5 to data[0] using built-in Atomic.Add for double.
// Expected output: 10

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicAddDoubleKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> data,
        double value)
    {
        Atomic.Add(ref data[0], value);
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

        using var buffer = stream.Allocate1D<double>(1);
        buffer.MemSetToZero();
        stream.Launch(
            (Index1D)4,
            index => Kernels.AtomicAddDoubleKernel(index, buffer.View, 2.5));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        Console.WriteLine((int)data[0]);
    }
}
