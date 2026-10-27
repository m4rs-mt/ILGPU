// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SwitchContinueLatchCopies.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: SwitchContinueLatchCopies
// A switch case continues a loop whose latch updates two loop-carried
// values; the other cases fall through to more code before the latch.
// Each lane starts from x = index % 9; twelve lanes span two SIMD groups.
// Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SwitchContinueLatchCopiesKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int x = index.X % 9;
        int a = x;
        int b = 1;
        int i = 0;
        while (i < 10)
        {
            int t;
            switch ((a + i) % 4)
            {
                case 0:
                    b += a;
                    i++;
                    continue;
                case 1:
                    t = a * 3;
                    break;
                case 2:
                    t = b - a;
                    break;
                default:
                    t = a + b + i;
                    break;
            }
            a = (t + b) % 97;
            b = b * 2 % 89;
            i++;
        }
        output[index] = a * 1000 + b;
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

        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.SwitchContinueLatchCopiesKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
