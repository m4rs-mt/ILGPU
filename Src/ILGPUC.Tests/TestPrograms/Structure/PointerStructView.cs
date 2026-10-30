// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerStructView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PointerStructView
// Every thread stores the address of the next input element into a view and reads the
// element back through the view: a view of structs whose only field is a pointer, a
// view of structs with a pointer and a second field, and a view of nint addresses.
// Expected output: one line per kernel, the input rotated by one element (plus the
// second field, the thread index, for the structs with two fields).

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct Holder
{
    public int* P;
}

unsafe struct TaggedHolder
{
    public int* P;
    public int Tag;
}

static unsafe class Kernels
{
    public static void HolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<Holder, Stride1D.Dense> holders,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        fixed (int* p = &input[(index + 1) % input.IntLength])
            holders[index] = new Holder { P = p };
        output[index] = *holders[index].P;
    }

    public static void TaggedHolderKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<TaggedHolder, Stride1D.Dense> holders,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        fixed (int* p = &input[(index + 1) % input.IntLength])
            holders[index] = new TaggedHolder { P = p, Tag = index };
        output[index] = *holders[index].P + holders[index].Tag;
    }

    public static void AddressKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<nint, Stride1D.Dense> addresses,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        fixed (int* p = &input[(index + 1) % input.IntLength])
            addresses[index] = (nint)p;
        output[index] = *(int*)addresses[index];
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

        using var input = stream.Allocate1D<int>(4);
        input.View.CopyFromCPU(stream, new int[] { 1, 8, 15, 22 });
        using var holders = stream.Allocate1D<Holder>(4);
        using var tagged = stream.Allocate1D<TaggedHolder>(4);
        using var addresses = stream.Allocate1D<nint>(4);
        using var buffer = stream.Allocate1D<int>(12);
        buffer.View.MemSetToZero(stream);
        var v0 = buffer.View.SubView(0, 4);
        var v1 = buffer.View.SubView(4, 4);
        var v2 = buffer.View.SubView(8, 4);
        stream.Launch(
            (Index1D)4,
            index => Kernels.HolderKernel(index, input.View, holders.View, v0));
        stream.Launch(
            (Index1D)4,
            index => Kernels.TaggedHolderKernel(index, input.View, tagged.View, v1));
        stream.Launch(
            (Index1D)4,
            index => Kernels.AddressKernel(index, input.View, addresses.View, v2));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0; i < 3; i++)
            Console.WriteLine(string.Join(" ", result[(4 * i)..(4 * i + 4)]));
    }
}
