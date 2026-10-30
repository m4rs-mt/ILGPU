// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerFieldPadding.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PointerFieldPadding
// A local struct holds two structs whose fields are padded to their alignment: a
// pointer field and an int field, or ints and longs. The kernels store the fields
// of both and read a field of the second one through a pointer to the first one,
// in a helper and in the kernel.
// Expected output: one line per kernel, index + 7 for each of the four threads.

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct Item
{
    public int* Next;
    public int Value;
}

struct ItemPair
{
    public Item First;
    public Item Second;
}

struct Padded
{
    public int A;
    public long B;
    public int C;
    public long D;
}

struct PaddedPair
{
    public Padded First;
    public Padded Second;
}

static unsafe class Kernels
{
    static int ReadSecond(Item* items) => items[1].Value;

    public static void HelperKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        ItemPair pair;
        pair.First.Next = null;
        pair.First.Value = 3;
        pair.Second.Next = null;
        pair.Second.Value = index;
        data[index] = ReadSecond(&pair.First) + 7;
    }

    public static void LocalKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        ItemPair pair;
        pair.First.Next = null;
        pair.First.Value = 3;
        pair.Second.Next = null;
        pair.Second.Value = index;
        Item* items = &pair.First;
        data[index] = items[1].Value + 7;
    }

    static long ReadSecond(Padded* items) => items[1].D;

    public static void IntLongKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        PaddedPair pair;
        pair.First.A = 1;
        pair.First.B = 2;
        pair.First.C = 3;
        pair.First.D = 4;
        pair.Second.A = 5;
        pair.Second.B = 6;
        pair.Second.C = 7;
        pair.Second.D = index;
        data[index] = (int)ReadSecond(&pair.First) + 7;
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

        using var buffer = stream.Allocate1D<int>(12);
        var v0 = buffer.View.SubView(0, 4);
        var v1 = buffer.View.SubView(4, 4);
        var v2 = buffer.View.SubView(8, 4);
        stream.Launch((Index1D)4, index => Kernels.HelperKernel(index, v0));
        stream.Launch((Index1D)4, index => Kernels.LocalKernel(index, v1));
        stream.Launch((Index1D)4, index => Kernels.IntLongKernel(index, v2));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0; i < 3; i++)
            Console.WriteLine(string.Join(" ", result[(4 * i)..(4 * i + 4)]));
    }
}
