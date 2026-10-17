// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecursivePointerFieldAssignments.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: RecursivePointerFieldAssignments
// Recursive helpers read the variable of their caller through the pointer field of
// their structure parameter, assign the address of their own local variable to that
// field and pass the structure on to the call of themselves, in tail position and
// with work left after the call, for a structure with one field and a structure with
// two fields.
// Expected output: the results of the eight threads for one helper after the other.

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
    static int SumCallerPointers(int n, Pointer caller, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        sum += *caller.Value;
        caller.Value = &x;
        return SumCallerPointers(n - 1, caller, sum);
    }

    static int SumCallerNodes(int n, Node caller, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        sum += *caller.Value + caller.Depth;
        caller.Value = &x;
        caller.Depth = n;
        return SumCallerNodes(n - 1, caller, sum);
    }

    static int CountCallerPointers(int n, Pointer caller, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        int value = *caller.Value;
        caller.Value = &x;
        return 1 + CountCallerPointers(n - 1, caller, sum - 1 + value);
    }

    static int CountCallerNodes(int n, Node caller, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        int value = *caller.Value + caller.Depth;
        caller.Value = &x;
        return 1 + CountCallerNodes(n - 1, caller, sum - 1 + value);
    }

    public static void RecursivePointerFieldAssignmentsKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int start = 0;
        var pointer = new Pointer { Value = &start };
        var node = new Node { Value = &start, Depth = 0 };
        data[index] = SumCallerPointers(index + 1, pointer, 0);
        data[index + 8] = SumCallerNodes(index + 1, node, 0);
        data[index + 16] = CountCallerPointers(index + 1, pointer, 0);
        data[index + 24] = CountCallerNodes(index + 1, node, 0);
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

        using var buffer = stream.Allocate1D<int>(32);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)8,
            index => Kernels.RecursivePointerFieldAssignmentsKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
