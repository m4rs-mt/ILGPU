// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineWithInnerClosure.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: InlineWithInnerClosure
// Inline launch lambda that contains an inner closure (Func<int, int>)
// which captures a local variable. Tests that ClosureElimination works
// when the outer kernel is an inline lambda.
// Computes: sum = add(index) + add(index + 1) where add(x) = { sum += x; return sum; }
// Expected output: 1 3 5 7 (one per line)
//   index=0: sum = 0+0+1 = 1
//   index=1: sum = 0+1+2 = 3
//   index=2: sum = 0+2+3 = 5
//   index=3: sum = 0+3+4 = 7

using System;
using ILGPU;
using ILGPU.Runtime;

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(4);
        var view = buffer.View;

        stream.Launch((Index1D)4, index =>
        {
            int sum = 0;
            Func<int, int> add = x => { sum += x; return sum; };
            add(index);
            add(index + 1);
            view[index] = sum;
        });
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
