// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TwoClosuresCrossBlock.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: TwoClosuresCrossBlock
// Two separate closures (capturing a=10 and b=3) whose captured values are read from
// blocks other than the entry block, because each argument is a ternary.
//
// Regression guard: the former ClosureElimination pass decomposed a >= 2-field
// closure into entry-block allocas that were appended to the END of the rebuilt entry
// block, so SSAConstruction's CreateNull re-init overwrote the captured values for
// every reader outside that block — both captures silently read as 0 on all five
// backends. A single-capture closure never reached the pass (a 1-field display class
// collapses to a bare primitive) and a single-block kernel happened to read before the
// alloca, which is why the whole existing Lambda execution suite missed it.
//
// index.X == 0 -> (a + a) + (b * b) = (10 + 10) + (3 * 3)  = 29
// index.X == 1 -> (1 + a) + (1 * b) = (1 + 10)  + (1 * 3)  = 14
// index.X == 2 -> (2 + a) + (2 * b) = (2 + 10)  + (2 * 3)  = 18
// index.X == 3 -> (3 + a) + (3 * b) = (3 + 10)  + (3 * 3)  = 22
// Expected output: 29 14 18 22 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void TwoClosuresCrossBlockKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b)
    {
        Func<int, int> addA = x => x + a;
        Func<int, int> mulB = x => x * b;
        data[index] =
            addA(index.X > 0 ? index.X : a) +
            mulB(index.X > 0 ? index.X : b);
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
        stream.Launch((Index1D)4, index => Kernels.TwoClosuresCrossBlockKernel(index, buffer.View, 10, 3));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
