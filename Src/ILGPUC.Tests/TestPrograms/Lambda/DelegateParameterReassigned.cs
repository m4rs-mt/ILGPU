// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateParameterReassigned.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DelegateParameterReassigned
// A helper reassigns its delegate parameter in a branch and invokes it after
// the merge. The parameter is never passed by reference, so the frontend
// tracks the selected target through the SSA value of the parameter.
// Expected output: 1 2 20 30 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    private static int Apply(Func<int, int> f, int x)
    {
        if (x > 1)
            f = y => y * 10;
        return f(x);
    }

    public static void DelegateParameterReassignedKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = Apply(x => x + 1, index.X);
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
        stream.Launch((Index1D)4, index => Kernels.DelegateParameterReassignedKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
