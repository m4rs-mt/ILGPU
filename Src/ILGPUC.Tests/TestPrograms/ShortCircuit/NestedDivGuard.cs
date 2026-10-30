// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedDivGuard.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: NestedDivGuard
// A zero-guarded branch whose arm holds a conditional division:
// r = b != 0 ? (a > 3 ? a / b : 0) + 1 : 1 with a = 10 and b = index - 1
// covering a ZERO divisor (thread 1). The inner conditional keeps its branch to
// guard the division, so the outer one has to keep its branch as well.
// Expected output: -9 1 11 6 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NestedDivGuardKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int a = data[index];
        int b = index.X - 1;
        int r;
        if (b != 0)
        {
            int quotient = a > 3 ? a / b : 0;
            r = quotient + 1;
        }
        else
        {
            r = 1;
        }
        data[index] = r;
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

        using var buffer = stream.Allocate1D(new[] { 10, 10, 10, 10 });
        stream.Launch(
            (Index1D)4,
            index => Kernels.NestedDivGuardKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
