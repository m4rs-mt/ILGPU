// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerLaneViewAtomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PerLaneViewAtomic
// Each lane selects one of two sub-views by its lane index and atomically adds
// t + 1 to element 1 of its view: even lanes add 1 + 3 + 5 + 7 to data[1],
// odd lanes 2 + 4 + 6 + 8 to data[3].
// Expected output: 0 16 0 20

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PerLaneViewAtomicKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int lane = index.X;
        ArrayView<int> view = lane % 2 == 0
            ? data.BaseView.SubView(0, 2)
            : data.BaseView.SubView(2, 2);
        Atomic.Add(ref view[1], lane + 1);
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

        using var data = stream.Allocate1D<int>(4);
        data.MemSetToZero(stream);
        stream.Launch((Index1D)8, index => Kernels.PerLaneViewAtomicKernel(index, data.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", data.GetAsArray1D()));
    }
}
