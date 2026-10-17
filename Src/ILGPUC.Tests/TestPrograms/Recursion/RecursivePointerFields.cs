// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecursivePointerFields.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: RecursivePointerFields
// Recursive helpers pass the address of a local variable in the pointer field of a
// structure with one field and of a structure with two fields to the call of
// themselves, use the result of that call, and every call reads the variable of its
// caller through that field.
// Expected output: 0 0 2 4 5 10 9 18 14 28 20 40 27 54 35 70

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct Pointer
{
    public int* Value;
}

unsafe struct Node
{
    public int* Value;
    public int Depth;
}

static unsafe class Kernels
{
    static int CountCallerPointers(int n, Pointer caller, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        return 1 + CountCallerPointers(
            n - 1,
            new Pointer { Value = &x },
            sum - 1 + *caller.Value);
    }

    static int CountCallerNodes(int n, Node caller, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        return 1 + CountCallerNodes(
            n - 1,
            new Node { Value = &x, Depth = n },
            sum - 1 + *caller.Value + caller.Depth);
    }

    public static void RecursivePointerFieldsKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int start = 0;
        data[index * 2] = CountCallerPointers(
            index + 1,
            new Pointer { Value = &start },
            0);
        data[index * 2 + 1] = CountCallerNodes(
            index + 1,
            new Node { Value = &start, Depth = 0 },
            0);
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
        stream.Launch(
            (Index1D)8,
            index => Kernels.RecursivePointerFieldsKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
