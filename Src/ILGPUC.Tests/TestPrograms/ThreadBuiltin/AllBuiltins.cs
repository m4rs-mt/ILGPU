// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AllBuiltins.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: ThreadBuiltin AllBuiltins
// Kernel calls a [NoInline] helper that reads all four thread built-ins.
// Expected output: 0 1 2 3

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ComputeGlobalIndex()
    {
        int groupIdx = Group.Index;
        int gridIdx  = (int)Grid.Index;
        int groupDim = Group.Dimension;
        int gridDim  = (int)Grid.Dimension;

        int total  = gridDim * groupDim;
        int global = gridIdx * groupDim + groupIdx;
        return global < total ? global : total - 1;
    }

    public static void AllBuiltinsKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = ComputeGlobalIndex();
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.AllBuiltinsKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
