// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedStructInLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: NestedStructInLoop
// A nested struct is updated in a loop that lanes leave in different
// iterations.
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

    struct Outer
    {
        public S In;
        public long L;
    }

    static long Compute(int x)
    {
        var o = new Outer { In = new S { A = x, B = 1 }, L = 0 };
        for (int k = 0; k < x; k++)
        {
            o.In.A += k;
            o.L += o.In.A;
            if (o.L > 20)
                break;
        }
        return o.L * 100 + o.In.A;
    }

    public static void NestedStructInLoopKernel(
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
        stream.Launch((Index1D)12, index => Kernels.NestedStructInLoopKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
