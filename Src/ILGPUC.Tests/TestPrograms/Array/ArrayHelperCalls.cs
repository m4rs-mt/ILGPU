// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayHelperCalls.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: ArrayHelperCalls
// Every call of a helper that allocates an array returns a new array, also if the
// helper is inlined twice into the same kernel: the arrays of MakeArray do not
// share their elements and two new empty arrays are different, while the arrays
// of Array.Empty are the same array. Helpers that are not inlined and use their
// arrays only inside, return an array they allocate from a single call, return
// the arrays passed to them or return Array.Empty keep these arrays as well.
// Every thread writes eight results.
// Expected output (per thread i): 12, 0, 1, 2i + 1, 2i + 1, 3i + 1, 5, 101

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int[] MakeArray() => new int[1];

    static int[] MakeEmpty() => new int[0];

    static int[] Empty() => Array.Empty<int>();

    static int[] MakeFilled(int value)
    {
        var values = new int[2];
        values[0] = value;
        values[1] = value + 1;
        return values;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int SumOfPair(int value)
    {
        var values = new int[2];
        values[0] = value;
        values[1] = value + 1;
        return values[0] + values[1];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int[] MakeOnce(int value)
    {
        var values = new int[1];
        values[0] = value;
        return values;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int[] Same(int[] values) => values;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int[] NotInlinedEmpty() => Array.Empty<int>();

    public static void ArrayHelperCallKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int i = index.X;
        var a = MakeArray();
        var b = MakeArray();
        a[0] = 1;
        b[0] = 2;

        var c = MakeFilled(i);
        var d = MakeFilled(2 * i);

        int o = i * 8;
        output[o + 0] = a[0] * 10 + b[0];
        output[o + 1] = MakeEmpty() == MakeEmpty() ? 1 : 0;
        output[o + 2] = Empty() == Empty() ? 1 : 0;
        output[o + 3] = c[0] + c[1];
        output[o + 4] = SumOfPair(i);
        output[o + 5] = d[0] + c[1] + (SumOfPair(0) - 1);
        output[o + 6] = MakeOnce(5)[0];
        output[o + 7] = (Same(c) == c ? 1 : 0) + (Same(d) == c ? 10 : 0) +
            (NotInlinedEmpty() == NotInlinedEmpty() ? 100 : 0);
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

        using var output = stream.Allocate1D<int>(32);
        stream.Launch(
            (Index1D)4,
            index => Kernels.ArrayHelperCallKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
