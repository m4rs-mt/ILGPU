// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SwitchSharedCases.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: SwitchSharedCases
// A helper switch whose cases share bodies: two cases with one target, and
// several cases that reach the code after the switch like the default.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Select(int x)
    {
        int r = x * 2;
        switch (x)
        {
            case 0:
                return 11;
            case 2:
            case 4:
                r = x * 7 + 1;
                break;
            case 1:
            case 5:
                break;
            case 6:
                if (x > 20)
                    return 3;
                break;
            case 7:
                return r - 40;
        }
        return r + 100;
    }

    public static void SwitchSharedCasesKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = Select(index.X % 9);
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
        stream.Launch((Index1D)12, index => Kernels.SwitchSharedCasesKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
