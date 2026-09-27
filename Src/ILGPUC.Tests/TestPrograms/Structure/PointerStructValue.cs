// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerStructValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PointerStructValue
// A struct whose only field is a pointer or reference to an input element is read by
// value: a pointer struct passed to a helper, a ref struct read in the kernel and a
// ref struct passed to a helper.
// Expected output: one line per kernel, the four input values.

using System;
using ILGPU;
using ILGPU.Runtime;

unsafe struct Holder
{
    public int* P;

    public Holder(int* p)
    {
        P = p;
    }
}

ref struct RefHolder
{
    public ref int R;

    public RefHolder(ref int r)
    {
        R = ref r;
    }
}

static unsafe class Kernels
{
    static int Read(Holder holder) => *holder.P;

    static int Read(RefHolder holder) => holder.R;

    public static void HelperKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        fixed (int* p = &input[index])
            output[index] = Read(new Holder(p));
    }

    public static void RefKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        var holder = new RefHolder(ref input[index]);
        output[index] = holder.R;
    }

    public static void RefHelperKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = Read(new RefHolder(ref input[index]));
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
        using var buffer = stream.Allocate1D<int>(12);
        buffer.View.MemSetToZero(stream);
        var v0 = buffer.View.SubView(0, 4);
        var v1 = buffer.View.SubView(4, 4);
        var v2 = buffer.View.SubView(8, 4);
        stream.Launch(
            (Index1D)4, index => Kernels.HelperKernel(index, input.View, v0));
        stream.Launch(
            (Index1D)4, index => Kernels.RefKernel(index, input.View, v1));
        stream.Launch(
            (Index1D)4, index => Kernels.RefHelperKernel(index, input.View, v2));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0; i < 3; i++)
            Console.WriteLine(string.Join(" ", result[(4 * i)..(4 * i + 4)]));
    }
}
