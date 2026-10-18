// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivGuard.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DivGuard
// Zero-guarded division: b = index - 1 covers a ZERO divisor (thread 1); if the division is ever speculated above its guard, the CPU backend throws.
// Expected output: 0 0 1 1 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DivGuardKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int a = 10;
        int b = index.X - 1;
        if (b != 0 && a / b > 2)
            data[index] = 1;
        else
            data[index] = 0;
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.DivGuardKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
