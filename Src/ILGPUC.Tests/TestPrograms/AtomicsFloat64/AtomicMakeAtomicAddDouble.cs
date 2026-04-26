// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicMakeAtomicAddDouble.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: AtomicsFloat64 / AtomicMakeAtomicAddDouble
// 4 threads use Atomic.MakeAtomic with a delegate CAS to add 2.5 to data[0].
// Pins the 3-defect Family B.1 regression in the C-like emitter pipeline.
// Expected output: 10

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicMakeAtomicAddDoubleKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> data,
        double value)
    {
        Atomic.MakeAtomic(
            ref data[0],
            value,
            (current, val) => current + val,
            (ref double target, double compare, double val) =>
                Atomic.CompareExchange(ref target, compare, val));
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
            index => Kernels.AtomicMakeAtomicAddDoubleKernel(index, buffer.View, 2.5));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        Console.WriteLine((int)data[0]);
    }
}
