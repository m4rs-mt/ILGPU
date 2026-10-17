// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LambdaParameterArgument.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LambdaParameterArgument
// Launch lambdas whose single call passes values computed from the lambda
// parameter (index.GridIndex, index.GroupIndex, i * 3) instead of the
// parameter itself. These values only exist inside the kernel, so the lambdas
// are lifted as inline kernels instead of launching the called method.
// Expected output: 0 1 2 3 10 11 12 13 / 0 3 6 9

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WriteGrouped(
        ArrayView1D<int, Stride1D.Dense> data,
        long gridIndex,
        int groupIndex) =>
        data[(int)gridIndex * 4 + groupIndex] = (int)gridIndex * 10 + groupIndex;

    public static void Store(
        ArrayView1D<int, Stride1D.Dense> data,
        int index,
        int value) =>
        data[index] = value;
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var grouped = stream.Allocate1D<int>(8);
        var groupedView = grouped.View;
        var config = new KernelConfig(2, 4);
        stream.Launch(config, index =>
            Kernels.WriteGrouped(groupedView, index.GridIndex, index.GroupIndex));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", grouped.GetAsArray1D()));

        using var scaled = stream.Allocate1D<int>(4);
        var scaledView = scaled.View;
        stream.Launch(scaledView.IntExtent, i => Kernels.Store(scaledView, i, i * 3));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", scaled.GetAsArray1D()));
    }
}
