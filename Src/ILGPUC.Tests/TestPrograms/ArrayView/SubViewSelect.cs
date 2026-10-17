// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SubViewSelect.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SubViewSelect
// Every thread selects one of two sub-views of a kernel parameter depending on
// its index, with a conditional expression and with if/else, and uses the
// selected view directly, through its length, in a helper and in an atomic,
// and selects contiguous ArrayView sub-views to read from and write to.
// With 4 threads: data[i] = 3 * i + 1 for i < 64; even threads select
// data[0..32) and odd threads select data[32..48)
// SelectKernel:  v[t] * 100 + v.IntLength
// IfElseKernel:  v[t] + v.Length * 1000
// HelperKernel:  v[t] + v.IntLength
// AtomicKernel:  adds t + 1 to counters[t / 2] (even t) or counters[4 + t / 2]
//                (odd t) and prints all 8 counters
// ContiguousKernel: like SelectKernel with contiguous views of data, written to
//                output[t / 2] (even t) or output[2 + t / 2] (odd t)
// Expected output: 132 10016 732 10616 32001 16100 32007 16106 33 116 39 122 1 3 0 0 2 4 0 0 132 732 10016 10616

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SelectKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        var v = lane % 2 == 0 ? data.SubView(0, 32) : data.SubView(32, 16);
        output[index] = v[lane] * 100 + v.IntLength;
    }

    public static void IfElseKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        ArrayView1D<int, Stride1D.Dense> v;
        if (lane % 2 == 0)
            v = data.SubView(0, 32);
        else
            v = data.SubView(32, 16);
        output[index] = v[lane] + (int)v.Length * 1000;
    }

    static int Pick(ArrayView1D<int, Stride1D.Dense> v, int i) => v[i] + v.IntLength;

    public static void HelperKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        var v = lane % 2 == 0 ? data.SubView(0, 32) : data.SubView(32, 16);
        output[index] = Pick(v, lane);
    }

    public static void AtomicKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> counters)
    {
        int lane = index.X;
        var v = lane % 2 == 0 ? counters.SubView(0, 4) : counters.SubView(4, 4);
        Atomic.Add(ref v[lane / 2], lane + 1);
    }

    public static void ContiguousKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int lane = index.X;
        ArrayView<int> v = lane % 2 == 0 ? data.BaseView.SubView(0, 32) : data.BaseView.SubView(32, 16);
        ArrayView<int> o = lane % 2 == 0 ? output.BaseView.SubView(0, 2) : output.BaseView.SubView(2, 2);
        o[lane / 2] = v[lane] * 100 + v.IntLength;
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

        var values = new int[64];
        for (int i = 0; i < values.Length; i++)
            values[i] = 3 * i + 1;
        using var data = stream.Allocate1D(values);
        using var select = stream.Allocate1D<int>(4);
        using var ifElse = stream.Allocate1D<int>(4);
        using var helper = stream.Allocate1D<int>(4);
        using var counters = stream.Allocate1D(new int[8]);
        using var contiguous = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.SelectKernel(index, data.View, select.View));
        stream.Launch((Index1D)4, index => Kernels.IfElseKernel(index, data.View, ifElse.View));
        stream.Launch((Index1D)4, index => Kernels.HelperKernel(index, data.View, helper.View));
        stream.Launch((Index1D)4, index => Kernels.AtomicKernel(index, counters.View));
        stream.Launch((Index1D)4, index => Kernels.ContiguousKernel(index, data.View, contiguous.View));
        stream.Synchronize();

        foreach (var buffer in new[] { select, ifElse, helper, counters, contiguous })
        {
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
