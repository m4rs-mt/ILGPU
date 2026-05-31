// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructFromBranchingHelper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: StructFromBranchingHelper
// A helper builds a struct in divergent branches and returns it.
// Each lane computes the function on x = index % 9; twelve lanes span two
// SIMD groups. Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    struct S
    {
        public int A;
        public float B;
    }

    static S MakeS(int x)
    {
        var s = new S();
        if (x > 2)
        {
            s.A = x;
            s.B = 2;
        }
        else
        {
            s.A = -x;
            s.B = 5;
        }
        return s;
    }

    static long Compute(int x)
    {
        var s = MakeS(x);
        return s.A + (long)s.B;
    }

    public static void StructFromBranchingHelperKernel(
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
        stream.Launch((Index1D)12, index => Kernels.StructFromBranchingHelperKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
