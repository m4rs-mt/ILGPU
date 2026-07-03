// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IrreducibleLoopFixedBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: IrreducibleLoopFixedBuffer
// A helper fills a fixed-size buffer before a loop with two entry blocks and
// reads it inside one of them and after the loop.
// Expected output: one result per lane.

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct Buffer4
{
    public fixed int Data[4];
}

static class Kernels
{
    static unsafe int Compute(int lane)
    {
        Buffer4 buffer;
        buffer.Data[0] = lane;
        buffer.Data[1] = 2 * lane + 1;
        buffer.Data[2] = 3;
        buffer.Data[3] = 4;
        int s = 0, k = 0;
        if ((lane & 1) == 0)
            goto B;
    A:
        s += k + buffer.Data[k & 3];
        k++;
    B:
        s += 2 * k + 1;
        k++;
        if (k < lane % 9)
        {
            if ((k & 2) == 0)
                goto A;
            goto B;
        }
        return s * 10 + buffer.Data[1];
    }

    public static void IrreducibleLoopFixedBufferKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = Compute(index.X);
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

        using var output = stream.Allocate1D<int>(12);
        stream.Launch((Index1D)12, index => Kernels.IrreducibleLoopFixedBufferKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
