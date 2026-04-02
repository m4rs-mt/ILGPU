// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupAllReduceMaxRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupAllReduceMaxRuntime
// Input read from a runtime buffer. Uses XMath.Max method group.
// 4 threads, input = [3, 1, 4, 2], Group.AllReduce(max) → 4 for all.
// Expected output: 4 4 4 4

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GroupAllReduceMaxRuntimeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        int result = Group.AllReduce(value, int.MinValue, XMath.Max);
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

        int[] inputData = [3, 1, 4, 2];
        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.GroupAllReduceMaxRuntimeKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
