// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupInclusiveScanAddRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupInclusiveScanAddRuntime
// Input read from a runtime buffer so the optimizer cannot fold.
// 4 threads, input = [1, 2, 3, 4], Group.InclusiveScan(+, identity=0)
// Expected inclusive prefix sum: [1, 3, 6, 10]
// Expected output: 1 3 6 10

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupInclusiveScanAddRuntimeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        int result = Group.InclusiveScan(value, 0, (a, b) => a + b);
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

        int[] inputData = [1, 2, 3, 4];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.GroupInclusiveScanAddRuntimeKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
