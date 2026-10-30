// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedDivGuardCondition.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: NestedDivGuardCondition
// A zero-guarded branch whose arm divides in the condition of a conditional:
// r = b != 0 ? (a / b > 2 ? Hash(a) : 7) + 1 : 1 with a = 10 and b = index - 1
// covering a ZERO divisor (thread 1). The hash of 288 operations keeps the
// conditional from being folded into a compound condition, so both branches
// are left to the if-conversion: the inner one may become a predicate, the
// outer one guards the division in its condition.
// Expected output: 8 1 -240544589 -240544589 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    private static int Round(int x)
    {
        x = x * 31 ^ 7;
        x = x * 17 ^ 3;
        x = x * 13 ^ 5;
        x = x * 11 ^ 9;
        return x;
    }

    private static int Rounds4(int x) => Round(Round(Round(Round(x))));

    private static int Rounds12(int x) => Rounds4(Rounds4(Rounds4(x)));

    private static int Hash(int x) => Rounds12(Rounds12(Rounds12(x)));

    public static void NestedDivGuardConditionKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int a = data[index];
        int b = index.X - 1;
        int r;
        if (b != 0)
        {
            int inner = a / b > 2 ? Hash(a) : 7;
            r = inner + 1;
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
            index => Kernels.NestedDivGuardConditionKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
