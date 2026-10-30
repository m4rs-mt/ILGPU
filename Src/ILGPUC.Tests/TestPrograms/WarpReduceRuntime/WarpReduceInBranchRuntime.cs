// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpReduceInBranchRuntime.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WarpReduceInBranchRuntime
// Warp reductions and scans in one arm of a conditional expression combine the
// values of the lanes taking that arm only. 4 threads, input = [1, 2, 3, 4]
// read from a runtime buffer: lanes 2 and 3 all-reduce and scan 3 and 4, and
// lanes 0, 2 and 3 reduce 1, 3 and 4 into lane 0.
// Expected output: 1 2 7 7, 1 2 3 7 and 8 (one line per kernel)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AllReduceInBranchKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        output[index] = value > 2
            ? Warp.AllReduce(value, (a, b) => a + b)
            : value;
    }

    public static void InclusiveScanInBranchKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        output[index] = value > 2
            ? Warp.InclusiveScan(value, (a, b) => a + b)
            : value;
    }

    public static void ReduceInBranchKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int value = input[index];
        output[index] = value != 2
            ? Warp.Reduce(value, (a, b) => a + b)
            : value;
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
        using var allReduced = stream.Allocate1D<int>(4);
        using var scanned = stream.Allocate1D<int>(4);
        using var reduced = stream.Allocate1D<int>(4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.AllReduceInBranchKernel(
                index, input.View, allReduced.View));
        stream.Launch(
            (Index1D)4,
            index => Kernels.InclusiveScanInBranchKernel(
                index, input.View, scanned.View));
        stream.Launch(
            (Index1D)4,
            index => Kernels.ReduceInBranchKernel(
                index, input.View, reduced.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", allReduced.GetAsArray1D()));
        Console.WriteLine(string.Join(" ", scanned.GetAsArray1D()));
        Console.WriteLine(reduced.GetAsArray1D()[0]);
    }
}
