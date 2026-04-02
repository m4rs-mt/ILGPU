// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicExchange.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: AtomicExchange
// 4 threads atomically exchange data[0] with value=42.
// Expected output: 42

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicExchangeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        Atomic.Exchange(ref data[0], value);
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

        using var buffer = stream.Allocate1D<int>(1);
        buffer.MemSetToZero();
        stream.Launch((Index1D)4, index => Kernels.AtomicExchangeKernel(index, buffer.View, 42));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        Console.WriteLine(data[0]);
    }
}
