// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalAddressInStruct.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalAddressInStruct
// A struct holds the address of a local, or of a field of a local struct, and the
// kernel reads the local through it. Variants: a struct whose only field is the
// pointer (built by its constructor, by an object initializer, passed by value to a
// helper), a struct with a second field, a ref struct whose only field is a ref, and
// a pointer to a field of a local struct.
// Expected output: one line per kernel, 7 * index + 1 for each of the four threads.

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

unsafe struct PaddedHolder
{
    public int* P;
    public int Pad;
}

ref struct RefHolder
{
    public ref int R;

    public RefHolder(ref int r)
    {
        R = ref r;
    }
}

struct Pair
{
    public int A;
    public int B;
}

static unsafe class Kernels
{
    static int Compute(Index1D index) => 7 * index + 1;

    static int Read(Holder holder) => *holder.P;

    public static void ConstructorKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = Compute(index);
        var holder = new Holder(&x);
        data[index] = *holder.P;
    }

    public static void InitializerKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = Compute(index);
        var holder = new Holder { P = &x };
        data[index] = *holder.P;
    }

    public static void HelperKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = Compute(index);
        data[index] = Read(new Holder(&x));
    }

    public static void PaddedKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = Compute(index);
        var holder = new PaddedHolder { P = &x, Pad = 3 };
        data[index] = *holder.P;
    }

    public static void RefKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = Compute(index);
        var holder = new RefHolder(ref x);
        data[index] = holder.R;
    }

    public static void StructFieldKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var pair = new Pair { A = 3, B = Compute(index) };
        var holder = new Holder(&pair.B);
        data[index] = *holder.P;
    }

    public static void FieldAddressKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        var pair = new Pair { A = 3, B = Compute(index) };
        var holder = new PaddedHolder { P = &pair.B, Pad = pair.A };
        data[index] = *holder.P;
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

        using var buffer = stream.Allocate1D<int>(28);
        buffer.View.MemSetToZero(stream);
        var v0 = buffer.View.SubView(0, 4);
        var v1 = buffer.View.SubView(4, 4);
        var v2 = buffer.View.SubView(8, 4);
        var v3 = buffer.View.SubView(12, 4);
        var v4 = buffer.View.SubView(16, 4);
        var v5 = buffer.View.SubView(20, 4);
        var v6 = buffer.View.SubView(24, 4);
        stream.Launch((Index1D)4, index => Kernels.ConstructorKernel(index, v0));
        stream.Launch((Index1D)4, index => Kernels.InitializerKernel(index, v1));
        stream.Launch((Index1D)4, index => Kernels.HelperKernel(index, v2));
        stream.Launch((Index1D)4, index => Kernels.PaddedKernel(index, v3));
        stream.Launch((Index1D)4, index => Kernels.RefKernel(index, v4));
        stream.Launch((Index1D)4, index => Kernels.StructFieldKernel(index, v5));
        stream.Launch((Index1D)4, index => Kernels.FieldAddressKernel(index, v6));
        stream.Synchronize();

        var result = buffer.GetAsArray1D();
        for (int i = 0; i < 7; i++)
            Console.WriteLine(string.Join(" ", result[(4 * i)..(4 * i + 4)]));
    }
}
