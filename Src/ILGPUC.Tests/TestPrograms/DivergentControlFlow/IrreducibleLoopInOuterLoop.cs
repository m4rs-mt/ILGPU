// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IrreducibleLoopInOuterLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: IrreducibleLoopInOuterLoop
// A loop with two entry blocks nested in an ordinary loop: in every outer
// iteration, lanes enter the inner loop at either block depending on the
// outer counter.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static long Compute(int x)
    {
        long total = 0;
        for (int j = 0; j < 3; j++)
        {
            long s = j;
            int k = 0;
            if (((x + j) & 1) == 0)
                goto B;
        A:
            s += k;
            k++;
        B:
            s += 2 * k + 1;
            k++;
            if (k < x + j)
            {
                if ((k & 2) == 0)
                    goto A;
                goto B;
            }
            total = total * 7 + s;
        }
        return total;
    }

    public static void IrreducibleLoopInOuterLoopKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output)
    {
        output[index] = Compute(index.X % 9);
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

        using var output = stream.Allocate1D<long>(12);
        stream.Launch((Index1D)12, index => Kernels.IrreducibleLoopInOuterLoopKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
