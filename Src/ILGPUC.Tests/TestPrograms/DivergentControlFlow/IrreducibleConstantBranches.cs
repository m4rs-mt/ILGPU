// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IrreducibleConstantBranches.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: IrreducibleConstantBranches
// Loops with two entry blocks (goto into a loop) whose branches depend on a
// constant per instantiation, Interop.SizeOf<T>(). Optimization folds the
// constant branches, which removes the second entry or the back edge, or keeps
// the loop with a run-time exit condition.
// Expected output: per instantiation (int, long, short), one line per lane.
// disable: max_line_length

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Compute<T>(int x) where T : unmanaged
    {
        if (Interop.SizeOf<T>() == 4)
            goto L1;
        goto L2;
    L1:
        x += 1;
    L2:
        x *= 2;
        if (Interop.SizeOf<T>() == 8 && x < 100)
            goto L1;
        return x;
    }

    static int ComputeIntoLoop<T>(int x) where T : unmanaged
    {
        if (Interop.SizeOf<T>() != 4)
            goto L2;
    L1:
        x += 3;
    L2:
        x *= 2;
        if (Interop.SizeOf<T>() == 2 && x < 50)
            goto L1;
        return x;
    }

    static int Combine<T>(int x) where T : unmanaged =>
        Compute<T>(x) * 1000 + ComputeIntoLoop<T>(x);

    public static void IntKernel(Index1D index, ArrayView1D<int, Stride1D.Dense> output) =>
        output[index] = Combine<int>(index);

    public static void LongKernel(Index1D index, ArrayView1D<int, Stride1D.Dense> output) =>
        output[index] = Combine<long>(index);

    public static void ShortKernel(Index1D index, ArrayView1D<int, Stride1D.Dense> output) =>
        output[index] = Combine<short>(index);
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var output = stream.Allocate1D<int>(3);
        var view = output.View;
        stream.Launch((Index1D)3, i => Kernels.IntKernel(i, view));
        stream.Synchronize();
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);

        stream.Launch((Index1D)3, i => Kernels.LongKernel(i, view));
        stream.Synchronize();
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);

        stream.Launch((Index1D)3, i => Kernels.ShortKernel(i, view));
        stream.Synchronize();
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
