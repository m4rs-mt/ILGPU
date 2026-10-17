// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerFieldLoop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PointerFieldLoop
// A loop sums locals through the pointer field of a local struct with a second
// field, which holds the sum, and assigns the address of a local to the pointer
// field in every iteration: always the same local, or one of two locals selected
// per iteration.
// Expected output: one line per kernel, the sum of each of the eight threads.

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct Node
{
    public int* Value;
    public int Sum;
}

static unsafe class Kernels
{
    public static void AssignKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int a = index + 1;
        int b = 10;
        var node = new Node { Value = &a, Sum = 0 };
        for (int i = 0; i < index; i++)
        {
            node.Sum += *node.Value;
            node.Value = &b;
        }
        data[index] = node.Sum + *node.Value;
    }

    public static void SelectKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int a = index + 1;
        int b = 10;
        var node = new Node { Value = &a, Sum = 0 };
        for (int i = 0; i < index; i++)
        {
            node.Sum += *node.Value;
            node.Value = (i & 1) == 0 ? &b : &a;
        }
        data[index] = node.Sum + *node.Value;
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

        using var buffer = stream.Allocate1D<int>(16);
        buffer.View.MemSetToZero(stream);
        var v0 = buffer.View.SubView(0, 8);
        var v1 = buffer.View.SubView(8, 8);
        stream.Launch((Index1D)8, index => Kernels.AssignKernel(index, v0));
        stream.Launch((Index1D)8, index => Kernels.SelectKernel(index, v1));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0; i < 2; i++)
            Console.WriteLine(string.Join(" ", result[(8 * i)..(8 * i + 8)]));
    }
}
