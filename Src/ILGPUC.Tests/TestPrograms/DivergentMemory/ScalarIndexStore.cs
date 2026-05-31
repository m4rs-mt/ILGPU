// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ScalarIndexStore.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: ScalarIndexStore
// Stores to a uniform element in divergent branches: only the active lane may
// store its value, and a branch without active lanes must not store at all.
// Expected output: 51 0 14 0

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void ScalarIndexStoreKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int x = input[index];
        if (x == 5)
            output[0] = index * 10 + 1;
        if (x < 0)
            output[1] = 99;
        if (x > 4)
        {
            int old = Atomic.Add(ref output[2], 2);
            if (old < 0)
                output[3] = 99;
        }
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

        var values = new int[12];
        for (int i = 0; i < values.Length; i++)
            values[i] = i;
        using var input = stream.Allocate1D<int>(values.Length);
        input.View.CopyFromCPU(stream, values);
        using var output = stream.Allocate1D<int>(4);
        output.MemSetToZero(stream);
        stream.Launch((Index1D)values.Length, index => Kernels.ScalarIndexStoreKernel(index, input.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
