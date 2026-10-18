// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NaNComparisons.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NaNComparisons
// Ordered and unordered float comparisons must keep their NaN semantics:
// branches compile to bge.un/blt.un, constants on the left swap the operands.
// Inputs: 1, NaN, 0.5, 2 -> bit 0 x < 1 (branch), bit 1 x >= 1, bit 2 1 < x,
// bit 3 !(x > 1), bit 4 x == x, bit 5 x <= 0.75 (branch).
// Expected output: 26 8 57 22

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void NaNComparisonsKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> data, ArrayView1D<int, Stride1D.Dense> output)
    {
        float x = data[index];
        int result = 0;
        if (x < 1f)
            result += 1;
        else
            result += 0;
        if (x >= 1f)
            result += 2;
        bool constantLeft = 1f < x;
        if (constantLeft)
            result += 4;
        bool notGreater = !(x > 1f);
        if (notGreater)
            result += 8;
        if (x == x)
            result += 16;
        if (x <= 0.75f)
            result += 32;
        output[index] = result;
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

        using var data = stream.Allocate1D<float>(4);
        data.View.CopyFromCPU(stream, new float[] { 1.0f, float.NaN, 0.5f, 2.0f });
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.NaNComparisonsKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
