// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DoWhileDigits.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: DoWhileDigits
// A do-while loop carries three values and exits once the remaining value
// is zero; the results of the last iteration are used after the loop.
// Expected output: digit sum * 100 + digit count of x * 12345 + 7 per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DoWhileDigitsKernel(
        Index1D index, ArrayView1D<long, Stride1D.Dense> output)
    {
        long v = index.X % 9 * 12345L + 7;
        long sum = 0;
        int digits = 0;
        do
        {
            sum += v % 10;
            v /= 10;
            digits++;
        }
        while (v != 0);
        output[index] = sum * 100 + digits;
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
        stream.Launch((Index1D)12, index => Kernels.DoWhileDigitsKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
