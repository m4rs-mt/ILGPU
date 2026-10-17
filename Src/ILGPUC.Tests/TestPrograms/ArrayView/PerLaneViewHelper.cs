// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerLaneViewHelper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PerLaneViewHelper
// Each lane passes a sub-view of a different length to a helper method that
// sums the view in a loop over its length and stores the sum into the first
// element: lane t uses data[2t..2t + 2) for even t and data[2t..2t + 1)
// for odd t, with data[i] = i + 1.
// Expected output: 3 3 11 7 / 3 2 3 4 11 6 7 8

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Sum(ArrayView<int> view)
    {
        int sum = 0;
        for (int i = 0; i < view.IntLength; i++)
            sum += view[i];
        view[0] = sum;
        return sum;
    }

    public static void PerLaneViewHelperKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        ArrayView<int> view = lane % 2 == 0
            ? data.BaseView.SubView(lane * 2, 2)
            : data.BaseView.SubView(lane * 2, 1);
        output[lane] = Sum(view);
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

        var values = new int[8];
        for (int i = 0; i < values.Length; i++)
            values[i] = i + 1;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.PerLaneViewHelperKernel(index, data.View, output.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", output.GetAsArray1D()));
        Console.WriteLine(string.Join(" ", data.GetAsArray1D()));
    }
}
