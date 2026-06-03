// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoopCarriedLoadInHeader.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: LoopCarriedLoadInHeader
// A loop loads a value at its top, before its exit test, and adds a value
// derived from that load to a loop-carried sum. The derived value must be
// computed after the load of the same iteration.
// With 4 threads: input = [1,2,3,4,5,6], thread t sums (input[i] + 10)^2
// for i = 0..t+1
// Expected output: 265 434 630 855

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void LoopCarriedLoadInHeaderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int n = index.X + 2;
        int i = 0;
        int sum = 0;
        while (true)
        {
            int p = input[i] + 10;
            if (i >= n)
                break;
            sum += p * p;
            i++;
        }
        data[index] = sum;
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

        using var input = stream.Allocate1D<int>(6);
        input.View.CopyFromCPU(stream, new int[] { 1, 2, 3, 4, 5, 6 });
        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.LoopCarriedLoadInHeaderKernel(index, input.View, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
