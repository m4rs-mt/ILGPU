// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FixedBufferAfterRegion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: FixedBufferAfterRegion
// A fixed-size buffer is filled before a loop whose early exit jumps past
// the code after the loop; the buffer is read after both paths merge.
// Expected output: r * 100 + buffer[lane % 4] per lane, where r is 1, 3 for
// an early exit, or 12 after all ten iterations.

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct Buffer4
{
    public fixed int Data[4];
}

static class Kernels
{
    public static unsafe void FixedBufferAfterRegionKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int lane = index.X;
        Buffer4 buffer;
        buffer.Data[0] = lane;
        buffer.Data[1] = lane * 2;
        buffer.Data[2] = 3;
        buffer.Data[3] = 4;
        int r;
        if (lane % 3 == 0)
            r = 1;
        else
        {
            r = 2;
            for (int i = 0; i < 10; i++)
            {
                if (data[i + lane] > 40)
                {
                    r = 3;
                    goto merge;
                }
                r += 1;
            }
            r += 10;
        }
    merge:
        output[index] = r * 100 + buffer.Data[lane % 4];
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

        var values = new int[32];
        for (int i = 0; i < values.Length; i++)
            values[i] = i * 13 % 50;
        using var data = stream.Allocate1D(values);
        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.FixedBufferAfterRegionKernel(index, output.View, data.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
