// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoopSwitchContinueView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: LoopSwitchContinueView
// A loop reads a view inside a per-lane switch whose cases either continue
// with the next iteration or fall out of the switch on two separate paths,
// so the views passed through the loop are merged before the back edge.
// Some lanes also leave the loop early from a case. Twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LoopSwitchContinueViewKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output,
        ArrayView1D<int, Stride1D.Dense> input)
    {
        int s = index.X;
        for (int i = 0; i < 12; i++)
        {
            switch (input[i + index.X] % 4)
            {
                case 0:
                    s += 1;
                    continue;
                case 1:
                    s += 10;
                    break;
                case 2:
                    if (s > 900)
                        goto done;
                    s += 100;
                    break;
                default:
                    s -= 1;
                    break;
            }
            s = s * 2 % 1009;
        }
    done:
        output[index] = s;
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

        var values = new int[24];
        for (int i = 0; i < values.Length; i++)
            values[i] = (i * 7 + 3) % 11;
        using var input = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.LoopSwitchContinueViewKernel(index, output.View, input.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
