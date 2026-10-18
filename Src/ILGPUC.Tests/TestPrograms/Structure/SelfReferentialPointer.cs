// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SelfReferentialPointer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: SelfReferentialPointer
// Structs point to their own type: a linked list of locals walked in a loop, a node
// passed by value to a helper that indexes the pointer it reads from the node, a
// struct whose only field points to itself, and two structs pointing to each other.
// Expected output: one line per kernel, one value for each of the four threads:
//   ListKernel     index + 111
//   ValueKernel    index + 7
//   SelfKernel     2 * index
//   MutualKernel   index + 100

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct Node
{
    public Node* Next;
    public int Value;
}

struct NodePair
{
    public Node First;
    public Node Second;
}

unsafe struct Self
{
    public Self* Me;
}

unsafe struct Left
{
    public Right* Other;
    public int Value;
}

unsafe struct Right
{
    public Left* Other;
    public int Value;
}

static unsafe class Kernels
{
    public static void ListKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Node a, b, c;
        c.Next = null;
        c.Value = 100;
        b.Next = &c;
        b.Value = 10;
        a.Next = &b;
        a.Value = index + 1;

        int sum = 0;
        for (Node* node = &a; node != null; node = node->Next)
            sum += node->Value;
        data[index] = sum;
    }

    static int ReadSecond(Node node) => node.Next[1].Value + node.Value;

    public static void ValueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        NodePair pair;
        pair.First.Next = null;
        pair.First.Value = 3;
        pair.Second.Next = null;
        pair.Second.Value = index;

        Node head;
        head.Next = &pair.First;
        head.Value = 7;
        data[index] = ReadSecond(head);
    }

    public static void SelfKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Self self;
        self.Me = &self;
        data[index] = self.Me->Me == &self ? 2 * index : -1;
    }

    public static void MutualKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        Left left;
        Right right;
        left.Other = &right;
        left.Value = index;
        right.Other = &left;
        right.Value = 100;
        data[index] = left.Other->Other->Value + left.Other->Value;
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
        var v0 = buffer.View.SubView(0, 4);
        var v1 = buffer.View.SubView(4, 4);
        var v2 = buffer.View.SubView(8, 4);
        var v3 = buffer.View.SubView(12, 4);
        stream.Launch((Index1D)4, index => Kernels.ListKernel(index, v0));
        stream.Launch((Index1D)4, index => Kernels.ValueKernel(index, v1));
        stream.Launch((Index1D)4, index => Kernels.SelfKernel(index, v2));
        stream.Launch((Index1D)4, index => Kernels.MutualKernel(index, v3));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0; i < 4; i++)
            Console.WriteLine(string.Join(" ", result[(4 * i)..(4 * i + 4)]));
    }
}
